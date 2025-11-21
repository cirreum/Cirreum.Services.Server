namespace Cirreum.Clock;

sealed class DateTimeService(TimeProvider timeProvider) : IDateTimeClock {

	public TimeProvider TimeProvider => timeProvider;

	private string? _cachedIanaTimeZoneId = null;
	public string LocalTimeZoneId {
		get {

			if (_cachedIanaTimeZoneId != null) {
				return _cachedIanaTimeZoneId;
			}

			var tzId = TimeZoneInfo.Local.Id;

			// If on Linux/macOS, the ID is already IANA format
			if (!OperatingSystem.IsWindows()) {
				_cachedIanaTimeZoneId = tzId;
				return _cachedIanaTimeZoneId;
			}

			// Try built-in conversion (wrap in try/catch for safety on different platforms)
			try {
				if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tzId, out var ianaId)
					&& !string.IsNullOrEmpty(ianaId)) {
					_cachedIanaTimeZoneId = ianaId;
					return _cachedIanaTimeZoneId;
				}
			} catch {
				// Silently continue to fallback methods if this fails
			}

			// Fall back to dictionary
			if (IDateTimeClock.WindowsToIanaMap.TryGetValue(tzId, out var dictIanaId)) {
				_cachedIanaTimeZoneId = dictIanaId;
				return _cachedIanaTimeZoneId;
			}

			// Last resort
			_cachedIanaTimeZoneId = "Etc/UTC";
			return _cachedIanaTimeZoneId;
		}
	}

}