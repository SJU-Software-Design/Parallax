"""Packaging must consume the new build location before checking native binaries."""
import plistlib
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class PackagePathTests(unittest.TestCase):
    def test_new_repository_and_standalone_builds_reach_native_validation(self):
        for project_name, repository in [("all-stages", True), ("gallery", True), ("exported-project", False)]:
            with self.subTest(project=project_name), tempfile.TemporaryDirectory() as temporary:
                base = Path(temporary) / "한글 export with spaces"
                if repository:
                    (base / "scripts").mkdir(parents=True)
                    project = base / "projects" / project_name
                    delivery = base / "builds/macos" / project_name
                else:
                    project = base / project_name
                    delivery = project / "Builds/macos"
                script = project / "Tools/Build/Package-MacOS.py"
                script.parent.mkdir(parents=True)
                source = "all-stages" if project_name == "all-stages" else "gallery"
                shutil.copy2(ROOT / "projects" / source / "Tools/Build/Package-MacOS.py", script)
                app = delivery / "Parallax_Unity_v0.6_Key_macOS/ShadeLink.app/Contents"
                (app / "MacOS").mkdir(parents=True)
                managed = app / "Resources/Data/Managed"
                managed.mkdir(parents=True)
                (app / "Info.plist").write_bytes(plistlib.dumps({
                    "CFBundlePackageType": "APPL", "CFBundleIdentifier": "com.shadelink.prototype",
                    "CFBundleExecutable": "ShadeLink"}))
                (app / "MacOS/ShadeLink").write_bytes(b"invalid native player")
                (managed / "Assembly-CSharp.dll").write_bytes(b"fixture managed code")
                result = subprocess.run([sys.executable, str(script)], capture_output=True,
                                        text=True, encoding="utf-8", errors="replace")
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("Missing native player or Mono runtime libraries", result.stderr)
                self.assertFalse(list(delivery.glob("*.zip")), "Invalid player was packaged")


if __name__ == "__main__":
    unittest.main()
