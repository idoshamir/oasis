import html
import json
import logging
import sys
from pathlib import Path
from typing import NamedTuple
from urllib.parse import urljoin, urlparse

from google import genai
from google.genai.errors import APIError
import requests
from bs4 import BeautifulSoup
from pydantic import BaseModel, Field, ValidationError, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict
from tenacity import (
    RetryCallState,
    retry,
    retry_if_exception,
    stop_after_attempt,
    wait_exponential,
)

logger = logging.getLogger(__name__)

OASIS_BLOG_URL = "https://www.oasis.security/blog"
PROMPT_PATH = Path(__file__).with_name("prompts") / "summary.txt"
REQUEST_TIMEOUT_SECONDS = 30
USER_AGENT = "BlogScanner.Worker/1.0 (NHI Blog Digest)"
MAX_CONTENT_CHARS = 10_000
MAX_TITLE_LENGTH = 255
MAX_DESCRIPTION_LENGTH = 5000
TICKET_TITLE_PREFIX = "NHI Blog Digest: "
GEMINI_MAX_RETRIES = 3
GEMINI_RETRY_BASE_DELAY_SECONDS = 2.0
GEMINI_RETRYABLE_STATUS_CODES = frozenset({429, 500, 502, 503, 504})


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

    api_key: str = Field(alias="API_KEY")
    project_key: str = Field(alias="PROJECT_KEY")
    gemini_api_key: str = Field(alias="GEMINI_API_KEY")
    gemini_model: str = Field(default="gemini-2.5-flash", alias="GEMINI_MODEL")
    api_endpoint: str = Field(alias="API_ENDPOINT")

    @field_validator("gemini_api_key", mode="before")
    @classmethod
    def normalize_gemini_api_key(cls, value: object) -> str:
        if not isinstance(value, str):
            raise ValueError("GEMINI_API_KEY must be a string.")

        key = value.strip().strip('"').strip("'")
        if not key.startswith("AIza"):
            raise ValueError(
                "GEMINI_API_KEY must start with 'AIza'. "
                "Create one at https://aistudio.google.com/apikey"
            )
        if len(key) < 39:
            raise ValueError(
                "GEMINI_API_KEY looks truncated (expected about 39 characters). "
                "Paste the full key from https://aistudio.google.com/apikey"
            )

        return key


class BlogPost(NamedTuple):
    title: str
    link: str
    body: str


class CreateNhiFindingRequest(BaseModel):
    title: str
    description: str
    projectKey: str


class NhiFindingResponse(BaseModel):
    issueKey: str
    issueId: str
    ledgerId: str


class ErrorResponse(BaseModel):
    message: str
    code: str | None = None


def load_summary_prompt() -> str:
    if not PROMPT_PATH.is_file():
        raise RuntimeError(f"Prompt file not found: {PROMPT_PATH}")

    prompt_template = PROMPT_PATH.read_text(encoding="utf-8").strip()
    if "{content}" not in prompt_template:
        raise RuntimeError(f"Prompt file must contain a {{content}} placeholder: {PROMPT_PATH}")

    return prompt_template


def _is_blog_post_href(href: str) -> bool:
    path = urlparse(urljoin(OASIS_BLOG_URL, href)).path.rstrip("/")
    return path.startswith("/blog/") and path != "/blog"


def fetch_latest_post() -> BlogPost:
    headers = {"User-Agent": USER_AGENT}
    response = requests.get(
        OASIS_BLOG_URL,
        headers=headers,
        timeout=REQUEST_TIMEOUT_SECONDS,
    )
    response.raise_for_status()

    soup = BeautifulSoup(response.text, "html.parser")
    seen_links: set[str] = set()
    latest_link: str | None = None

    for anchor in soup.find_all("a", href=True):
        href = anchor["href"]
        if not _is_blog_post_href(href):
            continue

        full_link = urljoin(OASIS_BLOG_URL, href)
        if full_link in seen_links:
            continue

        seen_links.add(full_link)
        latest_link = full_link
        break

    if latest_link is None:
        raise RuntimeError("No blog posts found on the Oasis blog index page.")

    post_response = requests.get(
        latest_link,
        headers=headers,
        timeout=REQUEST_TIMEOUT_SECONDS,
    )
    post_response.raise_for_status()

    post_soup = BeautifulSoup(post_response.text, "html.parser")
    title_tag = post_soup.find("h1")
    title = title_tag.get_text(strip=True) if title_tag else ""

    if not title:
        og_title = post_soup.find("meta", property="og:title")
        title = og_title["content"].strip() if og_title and og_title.get("content") else ""

    content_node = post_soup.select_one(".w-richtext")
    body = content_node.get_text(" ", strip=True) if content_node else ""

    if not title or not body:
        raise RuntimeError(f"Latest blog post is missing a title or body: {latest_link}")

    title = html.unescape(title)
    logger.info("Fetched latest post: %s (%s)", title, latest_link)
    return BlogPost(title=title, link=latest_link, body=body)


def normalize_gemini_summary(raw: str) -> str:
    stripped = raw.strip()
    if not stripped.startswith("{"):
        return stripped

    try:
        payload = json.loads(stripped)
    except json.JSONDecodeError:
        return stripped

    if not isinstance(payload, dict):
        return stripped

    description = payload.get("description")
    if isinstance(description, str) and description.strip():
        return description.strip()

    return stripped


def _is_retryable_gemini_error(exc: BaseException) -> bool:
    return isinstance(exc, APIError) and exc.code in GEMINI_RETRYABLE_STATUS_CODES


def _log_gemini_retry(retry_state: RetryCallState) -> None:
    if retry_state.outcome is None:
        return

    exc = retry_state.outcome.exception()
    if not isinstance(exc, APIError):
        return

    logger.warning(
        "Gemini API call failed (attempt %d/%d, code=%s), retrying: %s",
        retry_state.attempt_number,
        GEMINI_MAX_RETRIES,
        exc.code,
        exc,
    )


@retry(
    retry=retry_if_exception(_is_retryable_gemini_error),
    stop=stop_after_attempt(GEMINI_MAX_RETRIES),
    wait=wait_exponential(
        multiplier=GEMINI_RETRY_BASE_DELAY_SECONDS,
        min=GEMINI_RETRY_BASE_DELAY_SECONDS,
        max=GEMINI_RETRY_BASE_DELAY_SECONDS * 4,
    ),
    before_sleep=_log_gemini_retry,
    reraise=True,
)
def _generate_gemini_content(client: genai.Client, model: str, prompt: str):
    return client.models.generate_content(model=model, contents=prompt)


def summarize_with_gemini(text: str, api_key: str, model: str) -> str:
    client = genai.Client(api_key=api_key)
    prompt_template = load_summary_prompt()
    prompt = prompt_template.format(content=text[:MAX_CONTENT_CHARS])

    try:
        response = _generate_gemini_content(client, model, prompt)
    except APIError as exc:
        raise RuntimeError(f"Gemini API call failed: {exc}") from exc

    summary = normalize_gemini_summary(response.text or "")
    if not summary:
        raise RuntimeError("Gemini returned an empty summary.")

    if len(summary) > MAX_DESCRIPTION_LENGTH:
        summary = summary[: MAX_DESCRIPTION_LENGTH - 3] + "..."

    logger.info("Generated summary (%d chars)", len(summary))
    return summary


def _build_ticket_title(post_title: str) -> str:
    ticket_title = f"{TICKET_TITLE_PREFIX}{post_title}"
    if len(ticket_title) <= MAX_TITLE_LENGTH:
        return ticket_title

    allowed_post_title_length = MAX_TITLE_LENGTH - len(TICKET_TITLE_PREFIX) - 3
    trimmed_post_title = post_title[:allowed_post_title_length].rstrip() + "..."
    return f"{TICKET_TITLE_PREFIX}{trimmed_post_title}"


def _build_ticket_description(summary: str, source_link: str) -> str:
    description = f"{summary}\n\nSource: {source_link}"
    if len(description) <= MAX_DESCRIPTION_LENGTH:
        return description

    return description[: MAX_DESCRIPTION_LENGTH - 3] + "..."


def create_jira_ticket(
    settings: Settings,
    title: str,
    summary: str,
    source_link: str,
) -> NhiFindingResponse:
    payload = CreateNhiFindingRequest(
        title=_build_ticket_title(title),
        description=_build_ticket_description(summary, source_link),
        projectKey=settings.project_key,
    )

    headers = {
        "X-Api-Key": settings.api_key,
        "Content-Type": "application/json",
        "Accept": "application/json",
    }
    response = requests.post(
        settings.api_endpoint,
        json=payload.model_dump(),
        headers=headers,
        timeout=REQUEST_TIMEOUT_SECONDS,
    )

    if response.status_code == 201:
        finding = NhiFindingResponse.model_validate(response.json())
        logger.info(
            "Created Jira ticket %s (issueId=%s, ledgerId=%s)",
            finding.issueKey,
            finding.issueId,
            finding.ledgerId,
        )
        return finding

    try:
        error = ErrorResponse.model_validate(response.json())
        raise RuntimeError(
            f"Failed to create ticket ({response.status_code}, {error.code or 'unknown'}): {error.message}"
        )
    except (ValidationError, ValueError):
        raise RuntimeError(
            f"Failed to create ticket: {response.status_code} - {response.text}"
        ) from None


def main() -> int:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    try:
        settings = Settings()
    except ValidationError as exc:
        logger.error("Missing or invalid environment configuration: %s", exc)
        return 1

    try:
        post = fetch_latest_post()
        summary = summarize_with_gemini(
            post.body, settings.gemini_api_key, settings.gemini_model
        )
        create_jira_ticket(settings, post.title, summary, post.link)
    except requests.RequestException as exc:
        logger.error("Network error: %s", exc)
        return 1
    except RuntimeError as exc:
        logger.error("%s", exc)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
