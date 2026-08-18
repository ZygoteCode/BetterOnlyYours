enum VaultErrorKind {
  notFound,
  invalidPassword,
  badSignature,
  unsupportedVersion,
  truncated,
  malformedPayload,
  permissionDenied,
  ioFailure,
}

/// Typed vault failure. The UI maps [kind] to actionable copy instead of
/// dumping raw exception text at the user.
class VaultException implements Exception {
  const VaultException(this.kind, this.message, {this.cause});

  final VaultErrorKind kind;
  final String message;
  final Object? cause;

  /// Short, user-facing headline.
  String get title => switch (kind) {
    VaultErrorKind.notFound => 'Vault file not found',
    VaultErrorKind.invalidPassword => 'Master password not accepted',
    VaultErrorKind.badSignature => 'This file is not a BetterOnlyYours vault',
    VaultErrorKind.unsupportedVersion => 'Unsupported vault version',
    VaultErrorKind.truncated => 'Vault file is incomplete',
    VaultErrorKind.malformedPayload => 'Vault contents could not be read',
    VaultErrorKind.permissionDenied => 'Access to the vault file was denied',
    VaultErrorKind.ioFailure => 'Vault could not be read or written',
  };

  /// What the user can actually do next.
  String get hint => switch (kind) {
    VaultErrorKind.notFound =>
      'Restore your vault file or create a new vault to continue.',
    VaultErrorKind.invalidPassword =>
      'Check for Caps Lock and keyboard layout. If the password is right, the '
          'file may have been modified on disk.',
    VaultErrorKind.badSignature =>
      'The file header does not match the vault format. Restore a backup copy.',
    VaultErrorKind.unsupportedVersion =>
      'The vault was written by a newer version of BetterOnlyYours. Update the '
          'app to open it.',
    VaultErrorKind.truncated =>
      'The file is shorter than a valid vault. Restore the .bak backup next to '
          'it.',
    VaultErrorKind.malformedPayload =>
      'Decryption succeeded but the contents are not valid vault data.',
    VaultErrorKind.permissionDenied =>
      'Close other apps using the file, or run from a folder you can write to.',
    VaultErrorKind.ioFailure =>
      'Check that the vault folder exists and has free space, then retry.',
  };

  @override
  String toString() => 'VaultException(${kind.name}): $message';
}
