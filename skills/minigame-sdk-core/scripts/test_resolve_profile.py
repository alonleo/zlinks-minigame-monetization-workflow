import unittest

from resolve_profile import ProfileError, resolve


class ResolveProfileTests(unittest.TestCase):
    def catalog(self):
        return {
            "schema_version": 1,
            "defaults": {"cdn_prefix": "common", "ads": {"banner": "base", "rewarded": "base-r"}},
            "platforms": {"wechat": {"cdn_prefix": "wx", "ads": {"banner": "wx-banner"}}},
            "variants": [{
                "variant_id": "skin_a",
                "display_name": "fallback",
                "app_id": "fallback-app",
                "ads": {"rewarded": "skin-r"},
                "platforms": {"wechat": {"display_name": "微信马甲A", "app_id": "wx-app"}},
            }],
        }

    def test_precedence_and_deep_merge(self):
        result = resolve(self.catalog(), "skin_a", "wechat")
        self.assertEqual("微信马甲A", result["display_name"])
        self.assertEqual("wx", result["cdn_prefix"])
        self.assertEqual("wx-banner", result["ads"]["banner"])
        self.assertEqual("skin-r", result["ads"]["rewarded"])
        self.assertEqual(64, len(result["profile_sha256"]))

    def test_hash_is_deterministic(self):
        first = resolve(self.catalog(), "skin_a", "wechat")
        second = resolve(self.catalog(), "skin_a", "wechat")
        self.assertEqual(first["profile_sha256"], second["profile_sha256"])

    def test_rejects_secret_material(self):
        catalog = self.catalog()
        catalog["defaults"]["client_secret"] = "forbidden"
        with self.assertRaises(ProfileError):
            resolve(catalog, "skin_a", "wechat")


if __name__ == "__main__":
    unittest.main()
