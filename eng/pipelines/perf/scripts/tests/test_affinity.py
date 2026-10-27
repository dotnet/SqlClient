# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""Regression tests for best-effort benchmark process affinity (standard library only)."""

import contextlib
import ctypes
from ctypes import wintypes
import io
import os
import subprocess
import sys
from types import SimpleNamespace
import unittest
from unittest import mock

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import interleave_perf  # noqa: E402


class AffinityTests(unittest.TestCase):
    """Verify the native ABI and diagnostics without changing the test runner's affinity."""

    def setUp(self):
        stack = contextlib.ExitStack()
        self.addCleanup(stack.close)
        self.proc = SimpleNamespace(pid=123, _handle=0x12345678)
        self.kernel32 = mock.Mock()
        self.kernel32.SetProcessAffinityMask.return_value = 1
        stack.enter_context(mock.patch.object(
            ctypes, "windll", SimpleNamespace(kernel32=self.kernel32), create=True))
        self.loader = stack.enter_context(
            mock.patch.object(ctypes, "WinDLL", return_value=self.kernel32, create=True))
        stack.enter_context(mock.patch.object(interleave_perf, "os", SimpleNamespace(name="nt")))
        self.stderr = stack.enter_context(contextlib.redirect_stderr(io.StringIO()))

    def test_windows_uses_pointer_sized_signature_and_arguments(self):
        """A high handle and the highest mask bit must survive marshaling unchanged."""
        bits = ctypes.sizeof(ctypes.c_size_t) * 8
        self.proc._handle = (1 << (bits - 1)) | 0x12345678
        interleave_perf.apply_affinity(self.proc, [0, 2, bits - 1])

        self.loader.assert_called_once_with("kernel32", use_last_error=True)
        setter = self.kernel32.SetProcessAffinityMask
        self.assertEqual(setter.argtypes, [wintypes.HANDLE, ctypes.c_size_t])
        self.assertIs(setter.restype, wintypes.BOOL)
        setter.assert_called_once()
        handle, mask = setter.call_args.args
        self.assertIsInstance(handle, wintypes.HANDLE)
        self.assertIsInstance(mask, ctypes.c_size_t)
        self.assertEqual(handle.value, self.proc._handle)
        self.assertEqual(mask.value, (1 << (bits - 1)) | 5)
        self.assertEqual(self.stderr.getvalue(), "")

    def test_windows_failure_reports_saved_error(self):
        """A failed native call must report its error code and text without raising."""
        self.kernel32.SetProcessAffinityMask.return_value = 0
        with mock.patch.object(ctypes, "get_last_error", return_value=5, create=True) as error:
            with mock.patch.object(ctypes, "FormatError", return_value="Access is denied.\r\n",
                                   create=True) as format_error:
                interleave_perf.apply_affinity(self.proc, [0, 2])

        error.assert_called_once_with()
        format_error.assert_called_once_with(5)
        warning = self.stderr.getvalue()
        self.assertIn("WARNING: SetProcessAffinityMask failed for pid 123", warning)
        self.assertIn("mask 0x5", warning)
        self.assertIn("[WinError 5] Access is denied.", warning)

    def test_windows_load_failure_remains_best_effort(self):
        """Interop setup failures must warn rather than abort the benchmark run."""
        self.loader.side_effect = OSError("DLL unavailable")
        interleave_perf.apply_affinity(self.proc, [0])
        self.assertIn("WARNING: could not pin pid 123", self.stderr.getvalue())
        self.assertIn("DLL unavailable", self.stderr.getvalue())

    def test_windows_out_of_word_cpu_is_not_truncated(self):
        """CPU indices outside a native mask must not silently select different CPUs."""
        for word_bytes in (4, 8):
            with self.subTest(word_bytes=word_bytes):
                self.kernel32.reset_mock()
                with mock.patch.object(ctypes, "sizeof", return_value=word_bytes):
                    interleave_perf.apply_affinity(self.proc, [0, word_bytes * 8])
                self.kernel32.SetProcessAffinityMask.assert_not_called()
        self.assertIn("WARNING:", self.stderr.getvalue())
        self.assertIn("without CPU pinning", self.stderr.getvalue())

    def test_empty_cpus_do_not_load_windows_api(self):
        """An empty CPU list leaves the process untouched."""
        interleave_perf.apply_affinity(self.proc, [])
        self.loader.assert_not_called()
        self.assertEqual(self.stderr.getvalue(), "")

    def test_linux_keeps_sched_setaffinity(self):
        """Linux still receives the child PID and CPU set, without loading Windows APIs."""
        linux = SimpleNamespace(name="posix", sched_setaffinity=mock.Mock())
        with mock.patch.object(interleave_perf, "os", linux):
            interleave_perf.apply_affinity(self.proc, [1, 3, 3])
        linux.sched_setaffinity.assert_called_once_with(123, {1, 3})
        self.loader.assert_not_called()
        self.assertEqual(self.stderr.getvalue(), "")

    def test_linux_failure_remains_best_effort(self):
        """Linux pinning failures retain the existing warning-only behavior."""
        linux = SimpleNamespace(
            name="posix", sched_setaffinity=mock.Mock(side_effect=OSError("not permitted")))
        with mock.patch.object(interleave_perf, "os", linux):
            interleave_perf.apply_affinity(self.proc, [1])
        self.assertIn("WARNING: could not pin pid 123", self.stderr.getvalue())
        self.assertIn("not permitted", self.stderr.getvalue())
        self.loader.assert_not_called()


@unittest.skipUnless(os.name == "nt", "Requires the Windows affinity APIs")
class WindowsAffinityTests(unittest.TestCase):
    """Exercise real Windows calls against only a disposable child process."""

    def test_child_affinity_matches_requested_cpu(self):
        """Read back affinity to verify pinning takes effect on the intended child."""
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        getter = kernel32.GetProcessAffinityMask
        getter.argtypes = [wintypes.HANDLE, ctypes.POINTER(ctypes.c_size_t),
                          ctypes.POINTER(ctypes.c_size_t)]
        getter.restype = wintypes.BOOL

        with subprocess.Popen([sys.executable, "-c", "import sys; sys.stdin.read()"],
                              stdin=subprocess.PIPE) as proc:
            try:
                handle = wintypes.HANDLE(int(proc._handle))
                process_mask = ctypes.c_size_t()
                system_mask = ctypes.c_size_t()
                self.assertTrue(getter(handle, ctypes.byref(process_mask),
                                       ctypes.byref(system_mask)), ctypes.get_last_error())
                self.assertNotEqual(process_mask.value, 0, "Child must have an available CPU")
                cpu = process_mask.value.bit_length() - 1

                with contextlib.redirect_stderr(io.StringIO()) as stderr:
                    interleave_perf.apply_affinity(proc, [cpu])
                self.assertEqual(stderr.getvalue(), "")
                self.assertTrue(getter(handle, ctypes.byref(process_mask),
                                       ctypes.byref(system_mask)), ctypes.get_last_error())
                self.assertEqual(process_mask.value, 1 << cpu)
            finally:
                proc.communicate(timeout=10)

    def test_invalid_handle_reports_real_windows_error(self):
        """A null handle verifies last-error capture from an actual failed native call."""
        with contextlib.redirect_stderr(io.StringIO()) as stderr:
            interleave_perf.apply_affinity(SimpleNamespace(pid=123, _handle=0), [0])
        self.assertIn("SetProcessAffinityMask failed for pid 123", stderr.getvalue())
        self.assertIn("[WinError 6]", stderr.getvalue())
        self.assertIn(ctypes.FormatError(6).strip(), stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
