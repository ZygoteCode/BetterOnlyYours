#include <flutter/dart_project.h>
#include <flutter/flutter_view_controller.h>
#include <windows.h>

#include <cstdlib>
#include <string>

#include "flutter_window.h"
#include "utils.h"

namespace {

// The Flutter engine reads its switches from FLUTTER_ENGINE_SWITCHES /
// FLUTTER_ENGINE_SWITCH_<n>. `flutter run` sets them; a directly launched
// build has none, so Impeller is requested explicitly here.
//
// Windows currently runs Impeller (OpenGL ES) whatever this switch says, so
// this is about intent: if a future SDK reintroduces a Skia default, the app
// keeps the backend it was built and tested against. A switch supplied by the
// caller is never overwritten, so `flutter run --no-enable-impeller` still
// reaches the engine untouched.
std::wstring ReadEnvironment(const wchar_t* name) {
  wchar_t* buffer = nullptr;
  size_t size = 0;
  if (_wdupenv_s(&buffer, &size, name) != 0 || buffer == nullptr) {
    return std::wstring();
  }
  std::wstring value(buffer);
  free(buffer);
  return value;
}

void RequestImpeller() {
  int switch_count = 0;
  const std::wstring count = ReadEnvironment(L"FLUTTER_ENGINE_SWITCHES");
  if (!count.empty()) {
    switch_count = _wtoi(count.c_str());
  }

  for (int i = 1; i <= switch_count; i++) {
    const std::wstring key = L"FLUTTER_ENGINE_SWITCH_" + std::to_wstring(i);
    if (ReadEnvironment(key.c_str()).find(L"enable-impeller") !=
        std::wstring::npos) {
      return;  // Already decided by the caller.
    }
  }

  const std::wstring key =
      L"FLUTTER_ENGINE_SWITCH_" + std::to_wstring(switch_count + 1);
  _wputenv_s(key.c_str(), L"enable-impeller=true");
  _wputenv_s(L"FLUTTER_ENGINE_SWITCHES",
             std::to_wstring(switch_count + 1).c_str());
}

}  // namespace

int APIENTRY wWinMain(_In_ HINSTANCE instance, _In_opt_ HINSTANCE prev,
                      _In_ wchar_t *command_line, _In_ int show_command) {
  // Attach to console when present (e.g., 'flutter run') or create a
  // new console when running with a debugger.
  if (!::AttachConsole(ATTACH_PARENT_PROCESS) && ::IsDebuggerPresent()) {
    CreateAndAttachConsole();
  }

  // Initialize COM, so that it is available for use in the library and/or
  // plugins.
  ::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

  // Must happen before the engine is created.
  RequestImpeller();

  flutter::DartProject project(L"data");

  std::vector<std::string> command_line_arguments =
      GetCommandLineArguments();

  project.set_dart_entrypoint_arguments(std::move(command_line_arguments));

  FlutterWindow window(project);
  Win32Window::Point origin(10, 10);
  Win32Window::Size size(1180, 780);
  if (!window.Create(L"BetterOnlyYours", origin, size)) {
    return EXIT_FAILURE;
  }
  window.SetQuitOnClose(true);

  ::MSG msg;
  while (::GetMessage(&msg, nullptr, 0, 0)) {
    ::TranslateMessage(&msg);
    ::DispatchMessage(&msg);
  }

  ::CoUninitialize();
  return EXIT_SUCCESS;
}
