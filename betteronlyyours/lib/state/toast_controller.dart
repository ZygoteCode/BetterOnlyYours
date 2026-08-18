import 'dart:async';

import 'package:flutter/foundation.dart';

enum ToastKind { info, success, warning, error }

class ToastAction {
  const ToastAction({required this.label, required this.onPressed});

  final String label;
  final VoidCallback onPressed;
}

@immutable
class ToastMessage {
  const ToastMessage({
    required this.id,
    required this.title,
    required this.kind,
    this.detail,
    this.action,
  });

  final int id;
  final String title;
  final String? detail;
  final ToastKind kind;
  final ToastAction? action;
}

/// Application-wide, non-blocking notifications. Replaces scattered SnackBars
/// so feedback always appears in the same place with the same visual language.
class ToastController extends ChangeNotifier {
  final List<ToastMessage> _messages = <ToastMessage>[];
  final Map<int, Timer> _timers = <int, Timer>{};
  int _nextId = 1;

  static const int _maxVisible = 4;

  List<ToastMessage> get messages => List.unmodifiable(_messages);

  int show(
    String title, {
    String? detail,
    ToastKind kind = ToastKind.info,
    ToastAction? action,
    Duration duration = const Duration(seconds: 4),
  }) {
    final id = _nextId++;
    _messages.add(
      ToastMessage(
        id: id,
        title: title,
        detail: detail,
        kind: kind,
        action: action,
      ),
    );
    while (_messages.length > _maxVisible) {
      final removed = _messages.removeAt(0);
      _timers.remove(removed.id)?.cancel();
    }
    if (duration > Duration.zero) {
      _timers[id] = Timer(duration, () => dismiss(id));
    }
    notifyListeners();
    return id;
  }

  int success(String title, {String? detail, ToastAction? action}) =>
      show(title, detail: detail, kind: ToastKind.success, action: action);

  int error(String title, {String? detail, ToastAction? action}) => show(
    title,
    detail: detail,
    kind: ToastKind.error,
    action: action,
    duration: const Duration(seconds: 7),
  );

  int warning(String title, {String? detail, ToastAction? action}) => show(
    title,
    detail: detail,
    kind: ToastKind.warning,
    action: action,
    duration: const Duration(seconds: 6),
  );

  void dismiss(int id) {
    _timers.remove(id)?.cancel();
    final removed = _messages.indexWhere((m) => m.id == id);
    if (removed < 0) return;
    _messages.removeAt(removed);
    notifyListeners();
  }

  void clear() {
    for (final timer in _timers.values) {
      timer.cancel();
    }
    _timers.clear();
    if (_messages.isEmpty) return;
    _messages.clear();
    notifyListeners();
  }

  @override
  void dispose() {
    for (final timer in _timers.values) {
      timer.cancel();
    }
    _timers.clear();
    super.dispose();
  }
}
