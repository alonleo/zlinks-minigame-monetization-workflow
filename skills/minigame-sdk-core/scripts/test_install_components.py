import importlib.util
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("install_components.py")
SPEC = importlib.util.spec_from_file_location("install_components", MODULE_PATH)
install_components = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(install_components)


class InstallComponentsTests(unittest.TestCase):
    def test_core_manifest_contains_profile_runtime(self):
        with tempfile.TemporaryDirectory() as temporary:
            project = Path(temporary)
            pairs = install_components.collect(project)
        targets = {str(target.relative_to(project)).replace("\\", "/") for _, _, target in pairs}
        self.assertIn("Assets/Resources/platformTools/src/MiniGameProfile.cs", targets)
        self.assertIn("Assets/Resources/platformTools/src/MiniGameProfile.cs.meta", targets)


if __name__ == "__main__":
    unittest.main()
