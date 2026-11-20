import unittest

from validate_delivery_topology import ValidationError, normalize


class DeliveryTopologyTests(unittest.TestCase):
    def test_isolated_target(self):
        result = normalize({
            "delivery_topology": {"mode": "isolated_branches"},
            "branch_targets": [{
                "platform": "wechat",
                "platform_game_name": "测试游戏",
                "branch": "miniGame-wx-csyx",
            }],
        })
        self.assertEqual("isolated_branches", result["mode"])
        self.assertEqual("wechat", result["targets"][0]["platform"])

    def test_unified_target_resolves_official_name(self):
        result = normalize({
            "delivery_topology": {"mode": "unified_matrix", "unified_branch": "miniGame-unified"},
            "variant_catalog": [{"variant_id": "skin_a", "names": {"douyin": "马甲A"}}],
            "selected_targets": [{"variant_id": "skin_a", "platform": "douyin"}],
        })
        self.assertEqual("马甲A", result["targets"][0]["platform_game_name"])

    def test_modes_cannot_mix(self):
        with self.assertRaises(ValidationError):
            normalize({
                "delivery_topology": {"mode": "unified_matrix", "unified_branch": "miniGame-unified"},
                "branch_targets": [{"platform": "wechat"}],
                "variant_catalog": [{"variant_id": "original", "names": {"wechat": "原包"}}],
                "selected_targets": [{"variant_id": "original", "platform": "wechat"}],
            })

    def test_selected_platform_requires_name(self):
        with self.assertRaises(ValidationError):
            normalize({
                "delivery_topology": {"mode": "unified_matrix", "unified_branch": "miniGame-unified"},
                "variant_catalog": [{"variant_id": "original", "names": {"wechat": "原包"}}],
                "selected_targets": [{"variant_id": "original", "platform": "douyin"}],
            })


if __name__ == "__main__":
    unittest.main()
