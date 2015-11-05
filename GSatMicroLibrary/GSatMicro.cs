using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace GSatMicroLibrary
{
    public enum GSatMicroSetting : int
    {
        GPSHDOP = 1,
        GPSTimeout = 2,
        IridiumTxTimeout = 3,
        IridiumSignalTimeout = 4,
        IridiumTxRetries = 5,
        SleepInterval = 6,
        SOSSleepInterval = 7,
        SleepWhenPowered = 8,
        LEDMask = 9,
        KeepRadioAwake = 10,
        IncludeAltitude = 11,
        GPSSettle = 12,
        LowBatOff = 16,
        GPSHibernateSleep = 17,
        CacheReports = 18,
        MovingSleepInterval = 19,
        MovingThreshSpeed = 20,
        RequireEncryptedMT = 21,
        GPSOnAlways = 22,
        SleepWithBat = 23,
        IncludeSeconds = 24,
        ReportTenByteFormat = 25,
    }

    public class GSatMicroMessage
    {

    }

    public class GSatMicro
    {
        public const int CHAR_LIMIT = 226; // 255 byte firmware payload limit, 11 bytes for wrapping, 16 bytes for encryption (255-11-16)
        private const int ASSUMED_VERSION = 6;

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        
        /// <summary>
        /// Encrypt the message using the passed key and pre-calculated nonce.
        /// </summary>
        /// <param name="messageBytes"></param>
        /// <param name="encryptionKeyHex"></param>
        /// <param name="nonceBytes"></param>
        /// <returns></returns>
        private static byte[] EncryptMessage(byte[] messageBytes, string encryptionKeyHex, byte[] nonceBytes)
        {
            if (string.IsNullOrEmpty(encryptionKeyHex))
                return messageBytes;
            var key = StringToByteArray(encryptionKeyHex);
            var cipher = new CcmBlockCipher(new AesFastEngine());
            var parameters = new CcmParameters(new KeyParameter(key), 64, nonceBytes, new byte[] {});
            //var parameters = new CcmParameters(new KeyParameter(key), 64, nonceBytes, System.Text.Encoding.UTF8.GetBytes("testing much data"));
            cipher.Init(true, parameters);
            var encryptedBytes = new byte[cipher.GetOutputSize(messageBytes.Length)];
            var res = cipher.ProcessBytes(messageBytes, 0, messageBytes.Length, encryptedBytes, 0);
            cipher.DoFinal(encryptedBytes, res);
            return encryptedBytes;
        }

        private static byte[] DecryptMessage(byte[] encryptedBytes, string encryptionKeyHex)
        {
            if (string.IsNullOrEmpty(encryptionKeyHex))
                return encryptedBytes;

            var headerBytes = encryptedBytes.Take(1).ToArray(); // 0xFF
            var nonceBytes = encryptedBytes.Skip(1).Take(7).ToArray();
            encryptedBytes = encryptedBytes.Skip(8).ToArray();
            var key = StringToByteArray(encryptionKeyHex);
            var cipher = new CcmBlockCipher(new AesFastEngine());
            var parameters = new CcmParameters(new KeyParameter(key), 64, nonceBytes, new byte[] { });
            cipher.Init(false, parameters);
            var plainBytes = new byte[cipher.GetOutputSize(encryptedBytes.Length)];
            var res = cipher.ProcessBytes(encryptedBytes, 0, encryptedBytes.Length, plainBytes, 0);
            cipher.DoFinal(plainBytes, res);

            return plainBytes;
        }

        public static byte[] DecryptAndVerifyMessage(byte[] encryptedBytes, string authKeyBase64, string encryptionKeyHex, bool fromMobile = false) {
            if(!fromMobile)
                encryptedBytes = encryptedBytes.Skip(1).ToArray(); // 0x0
            byte[] messageBytes = encryptedBytes;
            if (!string.IsNullOrEmpty(encryptionKeyHex))
            {
                messageBytes = DecryptMessage(messageBytes, encryptionKeyHex);
            }
            var numBytes = messageBytes.Length;

            // MT messages require HMAC
            if (!fromMobile)
            {
                if (!VerifyHMAC(messageBytes, authKeyBase64))
                    throw new ApplicationException("HMAC invalid for message.");
            
                // remove HMAC
                numBytes = messageBytes.Length - 10;
            }
            return messageBytes.Take(numBytes).ToArray();
        }

        public static byte[] EncryptMessage(byte[] messageBytes, string encryptionKeyHex, int messageId)
        {
            if (!string.IsNullOrEmpty(encryptionKeyHex))
            {
                // only perform encryption if the key is set
                var bytes = new List<byte>();
                bytes.Add(0);
                bytes.Add(0xFF);
                var nonceId = messageId.ToString().PadLeft(7, '0');
                if (nonceId.Length > 7)
                    nonceId = nonceId.Substring(nonceId.Length - 7, 7);
                var nonceBytes = System.Text.Encoding.UTF8.GetBytes(nonceId);
                messageBytes = messageBytes.Skip(1).ToArray(); // move 0x0 to beginning
                var encryptedBytes = EncryptMessage(messageBytes, encryptionKeyHex, nonceBytes);
                bytes.AddRange(nonceBytes);
                bytes.AddRange(encryptedBytes);
                messageBytes = bytes.ToArray();
            }
            return messageBytes;
        }

        /// <summary>
        /// Helper method to both wrap and encrypt to-mobile messages.
        /// </summary>
        /// <param name="messageBytes"></param>
        /// <param name="authKeyBase64"></param>
        /// <param name="encryptionKeyHex"></param>
        /// <param name="messageId"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static byte[] WrapAndEncryptMessage(byte[] messageBytes, string authKeyBase64, string encryptionKeyHex, int messageId, int? version = ASSUMED_VERSION)
        {
            messageBytes = WrapMessage(messageBytes.ToList(), authKeyBase64, version);
            messageBytes = EncryptMessage(messageBytes, encryptionKeyHex, messageId);
            return messageBytes;
        }

        public static bool VerifyHMAC(byte[] messageBytes, string authKeyBase64, int? version = ASSUMED_VERSION)
        {
            if (!version.HasValue || (version <= 1))
                return true;

            var numBytes = messageBytes.Length - 10;
            var original = messageBytes.Take(numBytes).ToArray();
            var hmac = messageBytes.Skip(numBytes).ToArray();

            var hashGen = CreateHMAC(original, Convert.FromBase64String(authKeyBase64));

            return hmac.SequenceEqual(hashGen);
        }

        private static byte[] CreateHMAC(byte[] messageBytes, byte[] authKey)
        {
            var hasher = new HMACSHA256(authKey);
            var hash = hasher.ComputeHash(messageBytes);
            var hash80 = new byte[10];
            Array.Copy(hash, hash80, hash80.Length); // hmac-sha256-80
            return hash80;
        }

        /// <summary>
        /// Wrap to-mobile message with HMAC auth key. All to-mobile messages must be wrapped before sending.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="version"></param>
        /// <param name="authKeyBase64"></param>
        /// <returns></returns>
        public static byte[] WrapMessage(List<byte> request, string authKeyBase64, int? version = ASSUMED_VERSION)
        {
            if ((!version.HasValue) || (version <= 1))
                return request.ToArray();

            // version 2+
            var wrapped = new List<byte>();
            wrapped.Add(0);
            wrapped.AddRange(request);

            // add HMAC auth key
            var hasher = new HMACSHA256(Convert.FromBase64String(authKeyBase64));
            var hash = hasher.ComputeHash(request.ToArray());
            var hash80 = new byte[10];
            Array.Copy(hash, hash80, hash80.Length); // hmac-sha256-80

            wrapped.AddRange(hash80.ToList());
            return wrapped.ToArray();
        }

        /// <summary>
        /// Creates to-mobile settings request message. Device should respond with current settings.
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static byte[] CreateSettingsRequestMessage(int? version = ASSUMED_VERSION)
        {
            if ((!version.HasValue) || (version < 2))
                return CreateTextMessage("cache.cache(cache.MO,gsattrack.encsettings())", version);

            return new byte[2] { 3, 0 };
        }

        /// <summary>
        /// Creates to-mobile version request message. Device should respond with current software version.
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public static byte[] CreateVersionRequestMessage(int? version = ASSUMED_VERSION)
        {
            if ((!version.HasValue) || (version < 2))
                throw new ArgumentException("version");

            return new byte[2] { 4, 0 };
        }

        /// <summary>
        /// Creates to-mobile text message.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static byte[] CreateTextMessage(string text, int? version = ASSUMED_VERSION)
        {
            if ((!version.HasValue) || (version < 2))
                return Encoding.ASCII.GetBytes(text);

            var msg = new List<byte>();
            msg.Add(1);
            var bin = Encoding.ASCII.GetBytes(text);
            if (bin.Length > CHAR_LIMIT) // 13 bytes for final wrapping
                throw new ArgumentOutOfRangeException("text");
            msg.Add((byte)bin.Length);
            msg.AddRange(bin);
            return msg.ToArray();
        }

        /// <summary>
        /// Creates to-mobile raw Lua command message.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static byte[] CreateCommandMessage(string command, int? version = ASSUMED_VERSION)
        {
            if ((!version.HasValue) || (version < 2))
                return Encoding.ASCII.GetBytes(command);

            var msg = new List<byte>();
            msg.Add(0);
            var bin = Encoding.ASCII.GetBytes(command);
            if (bin.Length > CHAR_LIMIT)
                throw new ArgumentOutOfRangeException("command");
            msg.Add((byte)bin.Length);
            msg.AddRange(bin);
            return msg.ToArray();
        }

        public static byte[] CreateUpdateSettingMessage(GSatMicroSetting setting, int value, int? version = ASSUMED_VERSION)
        {
            var msg = new List<byte>();
            if ((!version.HasValue) || (version < 2))
                return msg.ToArray();

            msg.Add((byte)2);
            msg.Add((byte)6);
            UInt16 settingNumber = Convert.ToUInt16(setting);
            Int32 settingValue = Convert.ToInt32(value);
            msg.AddRange(BitConverter.GetBytes(settingNumber)); // little endian
            msg.AddRange(BitConverter.GetBytes(settingValue)); // little endian
            return msg.ToArray();
        }

        public static int GetLEDBitMaskFromStates(GSatMicroLEDStates ledSettings)
        {
            bool[] bools = new bool[5];
            bools[0] = ledSettings.GPS;
            bools[1] = ledSettings.Message;
            bools[2] = ledSettings.Power;
            bools[3] = ledSettings.Satellite;
            bools[4] = ledSettings.Alarm;
            var bitArray = new BitArray(bools);
            int[] array = new int[1];
            bitArray.CopyTo(array, 0);
            int ledMask = array[0];
            return ledMask;
        }

        public static GSatMicroLEDStates GetLEDStatesFromBitMask(int ledMask) {
            var states = new GSatMicroLEDStates();
            states.GPS = (ledMask & 1) > 0;
            states.Message = (ledMask & (1 << 1)) > 0;
            states.Power = (ledMask & (1 << 2)) > 0;
            states.Satellite = (ledMask & (1 << 3)) > 0;
            states.Alarm  = (ledMask & (1 << 4)) > 0;
            return states;
        }

        public static GSatMicroMessage ParseFromMobileMessage(byte[] message, string authKeyBase64, string encryptionKeyHex, DateTime? packetTimeUtc = null, int? version = ASSUMED_VERSION)
        {
            byte type = message[0];
            if ((type == 255) && !string.IsNullOrEmpty(encryptionKeyHex))
            {
                message = DecryptAndVerifyMessage(message, authKeyBase64, encryptionKeyHex, true);
                type = message[0];
            } else if (type == 255) {
                throw new ApplicationException("An encryption key must be specified to decrypt encrypted message.");
            }
            var payload = message.Skip(1).ToArray();
            switch (type)
            {
                case 0:
                case 128:
                    throw new ApplicationException("GSE Proprietary Format Not Supported");

                case 1: // text message
                    return GSatMicroTextMessage.ParseTextMessage(payload);
                    
                case 2: // settings message
                    return GSatMicroSettings.ParseMessage(payload);
                    
                case 3: // software version, now included with settings
                    break;

                case 4: // 10-byte format - requires knowing date message was submitted
                    return GSatMicroPosition.ParseTenBytePosition(payload, packetTimeUtc);
                    
                case 5: // 18-byte format
                    return GSatMicroPosition.ParseEighteenBytePosition(payload);
                case 255:
                    throw new ApplicationException("Encrypted message. Must be successfully decrypted before parsing.");
                default:
                    throw new ApplicationException(string.Format("Unknown message type: {0}", type));
            }
            return null;
        }
    }
}
