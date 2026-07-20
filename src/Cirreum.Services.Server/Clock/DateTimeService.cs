namespace Cirreum.Clock;

sealed class DateTimeService(TimeProvider timeProvider) : IDateTimeClock {

	public TimeProvider TimeProvider => timeProvider;

	private string? _cachedIanaTimeZoneId = null;
	public string LocalTimeZoneId {
		get {

			if (this._cachedIanaTimeZoneId != null) {
				return this._cachedIanaTimeZoneId;
			}

			var tzId = TimeZoneInfo.Local.Id;

			// If on Linux/macOS, the ID is already IANA format
			if (!OperatingSystem.IsWindows()) {
				this._cachedIanaTimeZoneId = tzId;
				return this._cachedIanaTimeZoneId;
			}

			// Try built-in conversion (wrap in try/catch for safety on different platforms)
			try {
				if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tzId, out var ianaId)
					&& !string.IsNullOrEmpty(ianaId)) {
					this._cachedIanaTimeZoneId = ianaId;
					return this._cachedIanaTimeZoneId;
				}
			} catch {
				// Silently continue to fallback methods if this fails
			}

			// Fall back to dictionary
			if (IDateTimeClock.WindowsToIanaMap.TryGetValue(tzId, out var dictIanaId)) {
				this._cachedIanaTimeZoneId = dictIanaId;
				return this._cachedIanaTimeZoneId;
			}

			// Last resort
			this._cachedIanaTimeZoneId = "Etc/UTC";
			return this._cachedIanaTimeZoneId;
		}
	}

}