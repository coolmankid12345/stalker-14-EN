#!/usr/bin/env python3

"""
Parses changelog entries from PR body and updates the changelog.

Expected environment variables:
- PR_BODY: The pull request body text
- PR_AUTHOR: The GitHub username of the PR author
- PR_URL: The URL of the pull request
- PR_NUMBER: The PR number (used for naming the part file)

Changelog section format in PR body:
## Changelog
- add: Added new feature
- fix: Fixed a bug
- tweak: Adjusted something
- remove: Removed old feature
"""

import os
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

import yaml

VALID_TYPES = {"add", "remove", "tweak", "fix"}
TYPE_MAP = {"add": "Add", "remove": "Remove", "tweak": "Tweak", "fix": "Fix"}

CHANGELOG_FILE = "Resources/Changelog/Changelog.yml"
PARTS_DIR = "Resources/Changelog/Parts"


def extract_changelog_section(body: str) -> str | None:
    """Extract the changelog section from PR body."""
    if not body:
        return None

    # Find ## Changelog section
    pattern = r"^## Changelog\s*\n(.*?)(?=^## |\Z)"
    match = re.search(pattern, body, re.MULTILINE | re.DOTALL)

    if not match:
        return None

    return match.group(1).strip()


def parse_author(section: str) -> str | None:
    """Parse author from the changelog section."""
    # Match "author: name" or "author: @name"
    pattern = r"^\s*author\s*:\s*@?(.+?)\s*$"

    for line in section.split("\n"):
        # Skip HTML comments
        if "<!--" in line or "-->" in line:
            continue

        match = re.match(pattern, line, re.IGNORECASE)
        if match:
            author = match.group(1).strip()
            # Skip placeholder values
            if author and author not in ("", "@"):
                return author

    return None


def parse_changelog_entries(section: str) -> list[dict]:
    """Parse changelog entries from the section text."""
    entries = []

    # Match lines like "- add: message" or "* fix: message"
    pattern = r"^\s*[-*]\s*(add|remove|tweak|fix)\s*:\s*(.+?)\s*$"

    for line in section.split("\n"):
        # Skip HTML comments
        if "<!--" in line or "-->" in line:
            continue

        match = re.match(pattern, line, re.IGNORECASE)
        if match:
            entry_type = match.group(1).lower()
            message = match.group(2).strip()

            # Skip placeholder entries
            if message.lower() == "your change here":
                continue

            if entry_type in VALID_TYPES and message:
                entries.append({"type": TYPE_MAP[entry_type], "message": message})

    return entries


def create_part_file(author: str, url: str, pr_number: str, changes: list[dict]) -> Path:
    """Create a changelog part file."""
    part_data = {
        "author": author,
        "changes": changes,
        "time": datetime.now(timezone.utc).isoformat(),
        "url": url,
    }

    part_path = Path(PARTS_DIR) / f"pr-{pr_number}.yml"

    with open(part_path, "w", encoding="utf-8") as f:
        yaml.safe_dump(part_data, f, default_flow_style=False, allow_unicode=True)

    return part_path


def run_update_changelog():
    """Run the update_changelog.py script to merge parts into main changelog."""
    result = subprocess.run(
        ["python", "Tools/update_changelog.py", CHANGELOG_FILE, PARTS_DIR],
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        print(f"Error running update_changelog.py: {result.stderr}", file=sys.stderr)
        sys.exit(1)

    print(result.stdout)


def main():
    pr_body = os.environ.get("PR_BODY", "")
    pr_author = os.environ.get("PR_AUTHOR", "Unknown")
    pr_url = os.environ.get("PR_URL", "")
    pr_number = os.environ.get("PR_NUMBER", "unknown")

    # Extract changelog section
    changelog_section = extract_changelog_section(pr_body)

    if not changelog_section:
        print("No changelog section found in PR body. Skipping.")
        return

    # Parse entries
    entries = parse_changelog_entries(changelog_section)

    if not entries:
        print("No valid changelog entries found. Skipping.")
        return

    # Parse author (use custom author if specified, otherwise fall back to PR author)
    custom_author = parse_author(changelog_section)
    author = custom_author if custom_author else pr_author

    print(f"Found {len(entries)} changelog entries from {author}")
    for entry in entries:
        print(f"  - {entry['type']}: {entry['message']}")

    # Create part file
    part_path = create_part_file(author, pr_url, pr_number, entries)
    print(f"Created part file: {part_path}")

    # Run update script to merge
    run_update_changelog()

    print("Changelog updated successfully!")


if __name__ == "__main__":
    main()
