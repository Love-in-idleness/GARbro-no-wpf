# Linux desktop

GARbro prefers the native Wayland backend. If it cannot connect to a usable
Wayland compositor, it falls back to X11. `GARBRO_LINUX_BACKEND=wayland` requires
native Wayland and reports initialization failure; `GARBRO_LINUX_BACKEND=x11`
selects X11 explicitly. The default is `auto`. The Wayland app ID is
`org.garbro.GARbro`.

The Avalonia 12.1.4 Wayland backend binds `zwp_text_input_v3` when the compositor
advertises it. GARbro's editable fields support preedit, committed text,
surrounding text, selection and caret geometry. Input-method setup belongs to the
desktop compositor/IME. No XIM environment-variable workaround is required for
native text-input-v3.

Avalonia 12.1.4 enables the text-input session before its initial surrounding
text is populated. On wlroots this omits the surrounding-text capability from
the enabled session. GARbro forwards the standard text client through a small
adapter and requests a reset after focus initialization, using public Avalonia
APIs. This re-enables the cached state. The behavior is covered by a real protocol
test, not by directly assigning text to simulate input.

The integration test uses an owned headless Sway compositor with XWayland
disabled. It checks preedit without premature insertion, Chinese commit,
UTF-8 byte offsets and deletion, then Japanese commit into a second field after
focus transfer. Native window rendering and extraction also run in this session.
The local reference is Sway 1.10.1 / wlroots 0.18.2; CI retains the protocol trace
and screenshot. Native portal file-picker dialogs and every desktop IME are not
covered by that test.

Linux needs the .NET 10 runtime, Wayland client/EGL/GLES, xkbcommon and fontconfig
libraries. On Debian/Ubuntu, the native Wayland dependencies include
`libwayland-client0 libwayland-egl1 libegl1 libgles2 libxkbcommon0 libfontconfig1`.
X11 libraries are also needed for the fallback. A compositor-appropriate
`xdg-desktop-portal` backend supplies file-picker integration.

For the regression test, install `sway libwayland-dev libxkbcommon-dev libgles2`
and a C compiler, build the solution, then run:

```sh
python3 tools/smoke-wayland.py
```

The test creates its own runtime directory and seat; it does not send input to
the user's active desktop. Protocol XML notices are retained under
`tests/wayland/protocols`; NWayland's MIT notice is in `docs/licenses`.
