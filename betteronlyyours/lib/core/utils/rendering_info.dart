import 'dart:ui' as ui;

/// Which rendering backend the engine started with.
///
/// Impeller is the default on Windows in the current SDK, and the runner also
/// requests it explicitly; this exists so the app can *show* what is actually
/// running instead of assuming.
class RenderingInfo {
  const RenderingInfo._();

  /// `dart:ui` only enables shader-based image filters on Impeller, so this is
  /// the supported way to detect the backend from Dart.
  static bool get isImpeller => ui.ImageFilter.isShaderFilterSupported;

  static String get backendLabel =>
      isImpeller ? 'Impeller (OpenGL ES)' : 'Skia';
}
