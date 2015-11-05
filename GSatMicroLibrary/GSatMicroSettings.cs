using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GSatMicroLibrary
{
    public class GSatMicroLEDStates
    {
        public bool GPS { get; set; }
        public bool Message { get; set; }
        public bool Power { get; set; }
        public bool Satellite { get; set; }
        public bool Alarm { get; set; }
    }

    /// <summary>
    /// GSatMicro Settings DTO
    /// </summary>
    public class GSatMicroSettings : GSatMicroMessage
    {
        public int? Version { get; set; }
        public int? SettingsVersion { get; set; }

        // version 1+
        /// <summary>
        /// 1 - g_hdop
        /// </summary>
        public double? GPSHDOP { get; set; }
        /// <summary>
        /// 2 - g_timeout
        /// </summary>
        public int? GPSTimeout { get; set; }
        /// <summary>
        /// 3 - i_tx_timeout
        /// </summary>
        public int? IridiumTxTimeout { get; set; }
        /// <summary>
        /// 4 - i_signal_timeout
        /// </summary>
        public int? IridiumSignalTimeout { get; set; }
        /// <summary>
        /// 5 - i_tx_retries
        /// </summary>
        public int? IridiumTxRetries { get; set; }
        /// <summary>
        /// 6 - sleep
        /// </summary>
        public int? SleepInterval { get; set; }
        /// <summary>
        /// 7 - sos_sleep
        /// </summary>
        public int? SOSSleepInterval { get; set; }
        /// <summary>
        /// 8 - sleep_w_power
        /// </summary>
        public bool? SleepWhenPowered { get; set; }
        /// <summary>
        /// 9 - led_mask
        /// </summary>
        public GSatMicroLEDStates LEDMask { get; set; }
        /// <summary>
        /// 10 - i_rx_always
        /// </summary>
        public bool? KeepRadioAwake { get; set; }
        /// <summary>
        /// 11 - tx_altitude
        /// </summary>
        public bool? IncludeAltitude { get; set; }
        /// <summary>
        /// 12 - g_settle
        /// </summary>
        public int? GPSSettle { get; set; }
        /// <summary>
        /// 16 - low_bat_off
        /// </summary>
        public bool? LowBatOff { get; set; }

        // version 2+
        /// <summary>
        /// 17 - g_hibernate_sleep
        /// </summary>
        public bool? GPSHibernateSleep { get; set; }
        /// <summary>
        /// 18 - cache_reports
        /// </summary>
        public bool? CacheReports { get; set; }

        // version 3+
        /// <summary>
        /// 19 - moving_sleep
        /// </summary>
        public int? MovingSleepInterval { get; set; }
        /// <summary>
        /// 20 - moving_thresh
        /// </summary>
        public int? MovingThreshSpeed { get; set; }
        /// <summary>
        /// 21 - require_encrypted_mt
        /// </summary>
        public bool? RequireEncryptedMT { get; set; }
        /// <summary>
        /// 22 - g_on_always
        /// </summary>
        public bool? GPSOnAlways { get; set; }

        // version 4+
        /// <summary>
        /// 23 - sleep_w_bat
        /// </summary>
        public bool? SleepWithBat { get; set; }

        // version 5+
        /// <summary>
        /// 24 - include_seconds
        /// </summary>
        public bool? IncludeSeconds { get; set; }

        // version 6+
        /// <summary>
        /// 25 - report_format, 0=gse, 1=10 byte, 2=18 byte
        /// </summary>
        public int? ReportFormat { get; set; }


        public static GSatMicroSettings ParseMessage(byte[] payload)
        {
            var settings = new GSatMicroSettings();
            settings.Version = payload[0];
            settings.SettingsVersion = payload[1];
            // remaining settings in signed integer format
            // skip first int32 bytes
            
            settings.GPSHDOP = BitConverter.ToInt32(payload, 6) / 10.0;
            settings.GPSTimeout = BitConverter.ToInt32(payload, 10);
            settings.IridiumTxTimeout = BitConverter.ToInt32(payload, 14);
            settings.IridiumSignalTimeout = BitConverter.ToInt32(payload, 18);
            settings.IridiumTxRetries = BitConverter.ToInt32(payload, 22);
            settings.SleepInterval = BitConverter.ToInt32(payload, 26);
            settings.SOSSleepInterval = BitConverter.ToInt32(payload, 30);
            settings.SleepWhenPowered = BitConverter.ToInt32(payload, 34) > 0;
            settings.LEDMask = GSatMicro.GetLEDStatesFromBitMask(BitConverter.ToInt32(payload, 38));
            settings.KeepRadioAwake = BitConverter.ToInt32(payload, 42) > 0;
            settings.IncludeAltitude = BitConverter.ToInt32(payload, 46) > 0;
            settings.GPSSettle = BitConverter.ToInt32(payload, 50);
            // skip 16 bytes - unused parameters
            
            if(payload.Length >= 70)
                settings.LowBatOff = BitConverter.ToInt32(payload, 66) > 0;
            if (payload.Length >= 74)
                settings.GPSHibernateSleep = BitConverter.ToInt32(payload, 70) > 0;
            if (payload.Length >= 78)
                settings.CacheReports = BitConverter.ToInt32(payload, 74) > 0;
            if (payload.Length >= 82)
                settings.MovingSleepInterval = BitConverter.ToInt32(payload, 78);
            if (payload.Length >= 86)
                settings.MovingThreshSpeed = BitConverter.ToInt32(payload, 82);
            if (payload.Length >= 90)
                settings.RequireEncryptedMT = BitConverter.ToInt32(payload, 86) > 0;
            if (payload.Length >= 94)
                settings.GPSOnAlways = BitConverter.ToInt32(payload, 90) > 0;
            if (payload.Length >= 98)
                settings.SleepWithBat = BitConverter.ToInt32(payload, 94) > 0;
            if (payload.Length >= 102)
                settings.IncludeSeconds = BitConverter.ToInt32(payload, 98) > 0;
            if (payload.Length >= 106)
                settings.ReportFormat = BitConverter.ToInt32(payload, 102);
            return settings;
        }
    }
}
