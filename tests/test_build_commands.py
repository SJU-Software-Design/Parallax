"""Exercise the build launchers without installing or launching Unity."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
PWSH = os.environ.get("PWSH_BINARY") or shutil.which("pwsh")

FAKE_UNITY = r'''
import json, os, sys
from pathlib import Path
args = sys.argv[1:]
Path(os.environ["PARALLAX_TEST_CAPTURE"]).write_text(json.dumps(args))
def value(flag):
    return args[args.index(flag) + 1]
project = Path(value("-projectPath"))
platform = "macOS" if value("-buildTarget") == "StandaloneOSX" else "Windows"
output = project.parent / "Builds" / platform
output.mkdir(parents=True, exist_ok=True)
Path(value("-logFile")).write_text("Fake Unity log\n")
exit_code = int(os.environ.get("PARALLAX_TEST_EXIT", "0"))
if exit_code:
    sys.exit(exit_code)
if not os.environ.get("PARALLAX_TEST_NO_ARTIFACT"):
    if platform == "macOS":
        app = output / "Parallax.app" / "Contents" / "MacOS"
        app.mkdir(parents=True, exist_ok=True)
        (app / "Parallax").write_text("fake executable")
    else:
        (output / "Parallax.exe").write_text("fake executable")
if not os.environ.get("PARALLAX_TEST_NO_REPORT"):
    result = os.environ.get("PARALLAX_TEST_RESULT", "Succeeded")
    (output / "build-result.txt").write_text("Result: " + result + "\n")
'''


class BuildCommandTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="parallax-build-tests-")
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.repo = self.base / "한글 repository with spaces"
        self.project = self.repo / "Parallax_Gallery_Source"
        settings = self.project / "ProjectSettings"
        settings.mkdir(parents=True)
        (settings / "ProjectVersion.txt").write_text("m_EditorVersion: 6000.3.24f1\n")
        tools = self.project / "Tools"
        tools.mkdir()
        shutil.copy2(ROOT / "Parallax_Gallery_Source/Tools/Common.ps1", tools)
        (self.repo / "scripts").mkdir()
        for name in ["build-mac.sh", "build-windows.ps1"]:
            source = ROOT / "scripts" / name
            if source.exists():
                shutil.copy2(source, self.repo / "scripts" / name)
        self.editor = self.base / "Unity editor with spaces"
        self.editor.write_text("#!" + sys.executable + "\n" + FAKE_UNITY)
        self.editor.chmod(0o755)
        self.capture = self.base / "arguments.json"
        self.env = os.environ.copy()
        self.env.update(UNITY_EDITOR_PATH=str(self.editor), PARALLAX_TEST_CAPTURE=str(self.capture))

    def run_build(self, platform, editor=None, **variables):
        name = "build-mac.sh" if platform == "macOS" else "build-windows.ps1"
        script = self.repo / "scripts" / name
        self.assertTrue(script.is_file(), f"Missing build command: {name}")
        command = ["/bin/bash", str(script)] if platform == "macOS" else [PWSH, "-NoProfile", "-File", str(script)]
        if editor:
            command += [str(editor)] if platform == "macOS" else ["-UnityEditor", str(editor)]
        return subprocess.run(command, cwd=self.base, env=dict(self.env, **variables), capture_output=True, text=True)

    def platforms(self):
        if PWSH:
            return ["macOS", "Windows"]
        return ["macOS"]

    def test_targets_and_paths_with_spaces_from_another_directory(self):
        for platform in self.platforms():
            with self.subTest(platform=platform):
                result = self.run_build(platform)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                args = json.loads(self.capture.read_text())
                target = "StandaloneOSX" if platform == "macOS" else "StandaloneWindows64"
                method = "BuildMacOS" if platform == "macOS" else "BuildWindows"
                self.assertEqual(args[args.index("-buildTarget") + 1], target)
                self.assertEqual(args[args.index("-standaloneBuildSubtarget") + 1], "Player")
                self.assertEqual(args[args.index("-executeMethod") + 1], "ShadeLink.Editor.GalleryPlatformBuild." + method)
                self.assertEqual(args[args.index("-projectPath") + 1], str(self.project))
                self.assertEqual(args[args.index("-logFile") + 1], str(self.repo / "Builds" / platform / "build.log"))
                self.assertIn("-batchmode", args)
                self.assertIn("-quit", args)

    def test_outputs_for_both_platforms_coexist(self):
        for platform in self.platforms():
            result = self.run_build(platform)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertTrue((self.repo / "Builds/macOS/Parallax.app/Contents/MacOS/Parallax").is_file())
        if PWSH:
            self.assertTrue((self.repo / "Builds/Windows/Parallax.exe").is_file())

    def test_explicit_editor_path_takes_precedence_over_environment(self):
        for platform in self.platforms():
            with self.subTest(platform=platform):
                result = self.run_build(platform, editor=self.editor, UNITY_EDITOR_PATH=str(self.base / "missing editor"))
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_unity_failure_is_not_reported_as_success(self):
        for platform in self.platforms():
            with self.subTest(platform=platform):
                result = self.run_build(platform, PARALLAX_TEST_EXIT="7")
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("build.log", result.stdout + result.stderr)

    def test_missing_editor_explains_required_version(self):
        for platform in self.platforms():
            with self.subTest(platform=platform):
                result = self.run_build(platform, UNITY_EDITOR_PATH=str(self.base / "missing editor"))
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("6000.3.24f1", result.stdout + result.stderr)

    def test_success_requires_an_artifact(self):
        for platform in self.platforms():
            with self.subTest(platform=platform):
                result = self.run_build(platform, PARALLAX_TEST_NO_ARTIFACT="1")
                self.assertNotEqual(result.returncode, 0)

    def test_failed_report_cannot_be_reported_as_success(self):
        for platform in self.platforms():
            with self.subTest(platform=platform):
                result = self.run_build(platform, PARALLAX_TEST_RESULT="Failed")
                self.assertNotEqual(result.returncode, 0)

    def test_stale_report_cannot_hide_a_missing_new_report(self):
        for platform in self.platforms():
            with self.subTest(platform=platform):
                first = self.run_build(platform)
                self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
                result = self.run_build(platform, PARALLAX_TEST_NO_REPORT="1")
                self.assertNotEqual(result.returncode, 0)


if __name__ == "__main__":
    unittest.main()
