import unittest

from apply_resolved_profile import ApplyError, apply_profile, profile_sha


class ApplyResolvedProfileTests(unittest.TestCase):
    def profile(self):
        profile = {
            "variant_id": "skin_a",
            "platform": "douyin",
            "display_name": "马甲A",
            "app_id": "tt-app-a",
        }
        profile["profile_sha256"] = profile_sha(profile)
        return profile

    def test_applies_identity_and_preserves_other_config(self):
        result = apply_profile(self.profile(), {"appid": "$APP_ID", "packOptions": {"ignore": []}}, "douyin")
        self.assertEqual("tt-app-a", result["appid"])
        self.assertEqual("马甲A", result["projectname"])
        self.assertIn("packOptions", result)

    def test_rejects_platform_mismatch(self):
        with self.assertRaises(ApplyError):
            apply_profile(self.profile(), {}, "wechat")

    def test_rejects_tampered_profile(self):
        profile = self.profile()
        profile["app_id"] = "tampered"
        with self.assertRaises(ApplyError):
            apply_profile(profile, {}, "douyin")


if __name__ == "__main__":
    unittest.main()
