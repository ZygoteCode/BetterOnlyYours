import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

/// Copies secrets to the clipboard and optionally clears them again.
///
/// The value BetterOnlyYours wrote is remembered, and the clipboard is only
/// wiped when it still holds exactly that value — copying something else in
/// the meantime is never destroyed.
class ClipboardService extends ChangeNotifier {
  Timer? _ticker;
  String? _trackedValue;
  String? _label;
  DateTime? _clearAt;
  int _totalSeconds = 0;

  String? get label => _label;
  bool get hasPendingClear => _clearAt != null;
  int get totalSeconds => _totalSeconds;

  int get secondsRemaining {
    final clearAt = _clearAt;
    if (clearAt == null) return 0;
    final remaining = clearAt.difference(DateTime.now()).inMilliseconds;
    return remaining <= 0 ? 0 : (remaining / 1000).ceil();
  }

  double get progress {
    if (_totalSeconds <= 0) return 0;
    return (secondsRemaining / _totalSeconds).clamp(0.0, 1.0);
  }

  /// Puts [value] on the clipboard. Returns false if the platform refused.
  Future<bool> copy(
    String value, {
    required String label,
    int clearAfterSeconds = 0,
  }) async {
    try {
      await Clipboard.setData(ClipboardData(text: value));
    } catch (_) {
      return false;
    }

    _label = label;
    _cancelTicker();

    if (clearAfterSeconds > 0) {
      _trackedValue = value;
      _totalSeconds = clearAfterSeconds;
      _clearAt = DateTime.now().add(Duration(seconds: clearAfterSeconds));
      _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
        if (secondsRemaining <= 0) {
          unawaited(clearIfUnchanged());
        } else {
          notifyListeners();
        }
      });
    } else {
      _trackedValue = null;
      _totalSeconds = 0;
      _clearAt = null;
    }

    notifyListeners();
    return true;
  }

  /// Clears the clipboard only when it still holds the tracked secret.
  Future<void> clearIfUnchanged() async {
    final tracked = _trackedValue;
    _cancelTicker();
    _clearAt = null;
    _totalSeconds = 0;
    _trackedValue = null;

    if (tracked != null) {
      try {
        final current = await Clipboard.getData(Clipboard.kTextPlain);
        if (current?.text == tracked) {
          await Clipboard.setData(const ClipboardData(text: ''));
        }
      } catch (_) {
        // Clipboard access can fail while another app holds it; leave it.
      }
    }

    notifyListeners();
  }

  /// Stops the countdown and keeps whatever is on the clipboard.
  void keepClipboard() {
    _cancelTicker();
    _trackedValue = null;
    _clearAt = null;
    _totalSeconds = 0;
    notifyListeners();
  }

  void _cancelTicker() {
    _ticker?.cancel();
    _ticker = null;
  }

  @override
  void dispose() {
    _cancelTicker();
    super.dispose();
  }
}
