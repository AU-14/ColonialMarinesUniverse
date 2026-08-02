from pathlib import Path
from tempfile import TemporaryDirectory
import unittest

from .audit import (
    derive_entity_keys,
    extract_placeholders,
    parse_fluent,
)


class YautjaLocalizationAuditTests(unittest.TestCase):
    def test_yaml_entities_have_explicit_english_and_russian_entries(self) -> None:
        from .audit import audit_repository

        root = Path(__file__).resolve().parents[3]
        result = audit_repository(root)
        missing_entity_errors = [
            error
            for error in result.errors
            if error.startswith("Missing en-US key: ent-") or error.startswith("Missing ru-RU key: ent-")
        ]

        self.assertEqual([], missing_entity_errors, "\n".join(missing_entity_errors))

    def test_known_runtime_keys_have_both_locales_and_matching_placeholders(self) -> None:
        from .audit import audit_repository

        root = Path(__file__).resolve().parents[3]
        result = audit_repository(root)
        required_keys = {
            "cmu-yautja-hivebreaker-requires-recent-death",
            "cmu-yautja-apc-siphon-verb",
            "cmu-yautja-self-destruct-dialog-title",
            "cmu-yautja-ceremonial-dagger-flay-first-pass-self",
            "cmu-yautja-hunt-console-blooding-cancelled",
            "cmu-yautja-lobby-translator-help-modern",
            "cmu-yautja-mark-already-marked",
        }

        for key in required_keys:
            self.assertFalse(any(error.startswith(f"Missing en-US key: {key}") for error in result.errors))
            self.assertFalse(any(error.startswith(f"Missing ru-RU key: {key}") for error in result.errors))

        self.assertFalse(
            any(error.startswith("Placeholder mismatch: cmu-yautja-") for error in result.errors),
            "Yautja placeholders must match between locales",
        )

    def test_parse_fluent_messages_and_attributes(self) -> None:
        with TemporaryDirectory() as directory:
            path = Path(directory) / "sample.ftl"
            path.write_text(
                "sample = {$user} has {$count} trophies.\n"
                "    .desc = {$user} owns them.\n",
                encoding="utf-8",
            )

            messages = parse_fluent(path)

        self.assertIn("sample", messages)
        self.assertIn("sample.desc", messages)
        self.assertEqual({"user", "count"}, messages["sample"].placeholders)
        self.assertEqual({"user"}, messages["sample.desc"].placeholders)

    def test_extract_placeholders_supports_selectors(self) -> None:
        self.assertEqual(
            {"target", "seconds"},
            extract_placeholders("{$target} ({$seconds ->[one] second *[other] seconds})"),
        )

    def test_derive_entity_keys_from_yaml_fields(self) -> None:
        self.assertEqual(
            {"ent-CMUTest", "ent-CMUTest.desc"},
            derive_entity_keys({"type": "entity", "id": "CMUTest", "name": "Test", "description": "Desc"}),
        )

    def test_repository_has_complete_yautja_localization(self) -> None:
        from .audit import audit_repository

        root = Path(__file__).resolve().parents[3]
        result = audit_repository(root)

        self.assertEqual([], result.errors, "\n".join(result.errors))


if __name__ == "__main__":
    unittest.main()
