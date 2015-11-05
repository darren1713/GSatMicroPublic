using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GSatMicroLibrary
{

    public enum GSatMicroReportFormat : int
    {
        GSE = 0,
        TenByte = 1,
        EighteenByte = 2,    
    }

    public class GSatMicroPosition : GSatMicroMessage
    {
        public int MagicNumber { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        /// <summary>
        /// Accuracy dependent on the format used
        /// </summary>
        public DateTime TimeUtc { get; set; }
        public GSatMicroReportFormat Format { get; set; }
        /// <summary>
        /// Number of satellites acquired for fix
        /// </summary>
        public int? Satellites { get; set; }
        /// <summary>
        /// Meters
        /// </summary>
        public double Altitude { get; set; }
        /// <summary>
        /// Kilometers per hour
        /// </summary>
        public double Speed { get; set; }
        /// <summary>
        /// Degrees, 0-360
        /// </summary>
        public double Course { get; set; }
        /// <summary>
        /// Estimated horizontal error in meters
        /// </summary>
        public double Accuracy { get; set; }
        /// <summary>
        /// Meters per second
        /// </summary>
        public double? ClimbRate { get; set; }
        public double BatteryPercentage { get; set; }

        public bool? IsCheckin { get; set; }
        public bool? IsDistress { get; set; }
        public bool? IsOnExternalPower { get; set; }

        private static BitArray GetBitsFromPayload(int numBits, int offset, BitArray payload)
        {
            BitArray extractedBits = new BitArray(numBits);
            for (var i = 0; i < numBits; i++)
            {
                extractedBits[i] = payload[offset + i];
            }
            return extractedBits;
        }

        // assumes 32-bit int
        private static int GetIntFromBitArray(BitArray bitArray)
        {
            if (bitArray == null)
                throw new ArgumentNullException("Argument cannot be null.");
            if (bitArray.Length > 32)
                throw new ArgumentException("Argument length shall be at most 32 bits.");

            int[] array = new int[1];
            bitArray.CopyTo(array, 0);
            return array[0];
        }

        private static float GetDoubleFromBitArray(BitArray bitArray)
        {
            if (bitArray == null)
                throw new ArgumentNullException("Argument cannot be null.");
            if (bitArray.Length > 32)
                throw new ArgumentException("Argument length shall be at most 32 bits.");

            byte[] array = new byte[4];
            bitArray.CopyTo(array, 0);
            return BitConverter.ToSingle(array, 0);
        }

        private static void OutputBits(BitArray array)
        {
            Console.WriteLine("Bits:");
            for (var i = 0; i < array.Count; i++)
            {
                Console.Write(array[i] ? "1" : "0");
            }
            Console.WriteLine();
        }

        public static void OutputBitsReverse(BitArray array)
        {
            Console.WriteLine("Bits Binary:");
            for (var i = array.Count - 1; i >= 0; i--)
            {
                Console.Write(array[i] ? "1" : "0");

            }
            Console.WriteLine();
        }

        private static int GetIntFromBits(int offset, int bits, byte[] bytes)
        {
            int result = 0;
            for (var i = 0; i < bits; i++)
            {
                int bitpos = offset + i;
                var bitByte = bytes[(int)Math.Floor((double)bitpos / 8D)];
                result = (result << 1) + ((bitByte >> (7 - (bitpos % 8))) & 0x01);
            }
            return result;
        }

        public static DateTime EPOCH_START = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static GSatMicroPosition ParseEighteenBytePosition(byte[] payload)
        {
            if (payload.Length < 18)
                throw new ApplicationException("Position must be at least 18 bytes in length.");
           
            var position = new GSatMicroPosition() { Format = GSatMicroReportFormat.EighteenByte };
            //var payloadBits = new BitArray(payload);
            //OutputBits(payloadBits);

            position.MagicNumber = GetIntFromBits(0, 3, payload);
            var longitude = GetIntFromBits(3, 26, payload);
            position.Longitude = (((double)longitude)/186413)-180;
            position.IsOnExternalPower = Convert.ToBoolean(GetIntFromBits(29, 1, payload));
            position.IsDistress = Convert.ToBoolean(GetIntFromBits(30, 1, payload));
            position.IsCheckin = Convert.ToBoolean(GetIntFromBits(31, 1, payload));

            position.TimeUtc = EPOCH_START.AddSeconds(GetIntFromBits(32, 29, payload));
            position.Satellites = GetIntFromBits(61, 3, payload);
            var latitude = GetIntFromBits(64, 25, payload);
            position.Latitude = (((double)latitude) / 186413) - 90;
            position.Course = GetIntFromBits(89, 6, payload) * 5;
            position.Accuracy = GetIntFromBits(95, 6, payload);
            position.ClimbRate = (GetIntFromBits(101, 11, payload) - (2 ^ 10)) / 20;
            position.BatteryPercentage = (GetIntFromBits(112, 5, payload) * 3);
            position.Speed = GetIntFromBits(117, 11, payload);
            position.Altitude = GetIntFromBits(128, 16, payload);
            return position;
        }

        public static GSatMicroPosition ParseTenBytePosition(byte[] payload, DateTime? packetDateUtc = null)
        {
            if (payload.Length < 10)
                throw new ApplicationException("Position must be at least 10 bytes in length.");
            var position = new GSatMicroPosition() { Format = GSatMicroReportFormat.TenByte };
            //var payloadBits = new BitArray(payload);
            //OutputBits(payloadBits);

            position.MagicNumber = GetIntFromBits(0, 3, payload);
            var longitude = GetIntFromBits(3, 23, payload);
            position.Longitude = (((double)longitude) / 23301) - 180;
            position.Course = GetIntFromBits(26, 6, payload) * 5;
            
            // hours since midnight + 2 minute intervals
            var time = GetIntFromBits(32, 10, payload);
            if (!packetDateUtc.HasValue)
                packetDateUtc = DateTime.UtcNow;
            var baseDateUtc = new DateTime(packetDateUtc.Value.Year, packetDateUtc.Value.Month, packetDateUtc.Value.Day, 0, 0, 0, DateTimeKind.Utc);
            position.TimeUtc = baseDateUtc.AddMinutes((time * 2));

            // if over 12 hours different, subtract a day as it crossed midnight
            if (position.TimeUtc.Subtract(packetDateUtc.Value).Duration() > TimeSpan.FromHours(12))
                position.TimeUtc = position.TimeUtc.AddDays(-1);

            var latitude = GetIntFromBits(42, 22, payload);
            position.Latitude = (((double)latitude) / 23301) - 90;
            position.Speed = GetIntFromBits(64, 6, payload);
            position.Altitude = GetIntFromBits(70, 10, payload) * 5;
            if (payload.Length >= 11)
            {
                position.BatteryPercentage = GetIntFromBits(80, 5, payload) * 3;
                position.IsOnExternalPower = Convert.ToBoolean(GetIntFromBits(85, 1, payload));
                position.IsCheckin = Convert.ToBoolean(GetIntFromBits(86, 1, payload));
                position.IsDistress = Convert.ToBoolean(GetIntFromBits(87, 1, payload));
            }
            return position;
        }
    }
}
