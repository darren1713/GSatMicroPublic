using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GSatMicroLibrary;

namespace GSatMicroLibraryTester
{
    class Program
    {
        static void Main(string[] args)
        {
            string authKey = "jSO9LnThTZ5nCWFBEmAxOuECbHY="; // device password
            //string encryptionKey = null; // must be non-null if encryption is enabled
            string encryptionKey = "37996fb42dde61e890d2d88b9b5a83d16f3d35876d778e3e064677c37b50c424"; // test value

            // To-Mobile messages
            int toMobileMessageId = 0;

            // Request current settings
            // Refer to http://www.gsattrack.com/wiki/GSatMicro.ashx#Settings_functions_28 for settings values
            var settingsRequest = GSatMicro.CreateSettingsRequestMessage();
            // Binary messages must be wrapped  with HMAC (password) and optionally encrypted
            var settingsPayload = GSatMicro.WrapAndEncryptMessage(settingsRequest, authKey, encryptionKey, toMobileMessageId++);
            Console.WriteLine(string.Format("Request Current Settings:\n{0}\n{1}\n", BitConverter.ToString(settingsPayload), settingsPayload.Length));

            // Update single setting - 19 bytes wrapped, 35 encrypted
            double gpsHDOP = 2.5D;
            var singleSettingUpdateRequest = GSatMicro.CreateUpdateSettingMessage(GSatMicroSetting.GPSHDOP, (int)Math.Round(gpsHDOP * 10D));
            var singleSettingPayload = GSatMicro.WrapAndEncryptMessage(singleSettingUpdateRequest, authKey, encryptionKey, toMobileMessageId++);
            var singleSettingDecrypted = GSatMicro.DecryptAndVerifyMessage(singleSettingPayload, authKey, encryptionKey, false);
            Console.WriteLine("Before Encryption:\n{0}\n", BitConverter.ToString(singleSettingUpdateRequest));
            Console.WriteLine("Wrapped/Encrypted:\n{0}\n", BitConverter.ToString(singleSettingPayload), singleSettingPayload.Length);
            Console.WriteLine("Decrypted:\n{0}\n", BitConverter.ToString(singleSettingDecrypted));

            // Update LED Settings (bitmask)
            var ledSettings = new GSatMicroLEDStates
            {
                Alarm = true,
                GPS = true,
                Message = false,
                Power = true,
                Satellite = true,
            };
            var ledMask = GSatMicro.GetLEDBitMaskFromStates(ledSettings);
            var ledSettingsRequest = GSatMicro.CreateUpdateSettingMessage(GSatMicroSetting.LEDMask, ledMask);
            var ledSettingsPayload = GSatMicro.WrapAndEncryptMessage(ledSettingsRequest, authKey, encryptionKey, toMobileMessageId++);
            Console.WriteLine(string.Format("LED Mask Update:\n{0}\n{1}\n", BitConverter.ToString(ledSettingsPayload), ledSettingsPayload.Length));

            // Update multiple settings
            var multipleSettingsRequest = new List<byte>();
            multipleSettingsRequest.AddRange(GSatMicro.CreateUpdateSettingMessage(GSatMicroSetting.GPSHDOP, (int)Math.Round(gpsHDOP * 10D)));
            multipleSettingsRequest.AddRange(GSatMicro.CreateUpdateSettingMessage(GSatMicroSetting.ReportTenByteFormat, 1));
            if (multipleSettingsRequest.ToArray().Length > 255)
            {
                // TODO: iterate over request and send individually
                throw new ApplicationException("Payload too large for satellite. Split into multiple requests.");
            }
            var multipleSettingsPayload = GSatMicro.WrapAndEncryptMessage(multipleSettingsRequest.ToArray(), authKey, encryptionKey, toMobileMessageId++);
            Console.WriteLine(string.Format("Multiple Setting Update:\n{0}\n", BitConverter.ToString(multipleSettingsPayload)));

            // From-Mobile messages

            Console.WriteLine("18 Byte Position");
            Console.WriteLine("----------------");
            byte[] micro18 = GSatMicro.StringToByteArray("08e6a1d00c1c0ee77cf251303800d000000e");
            Console.WriteLine("Position Bytes:\n{0}\n", BitConverter.ToString(micro18));
            var position = GSatMicroPosition.ParseEighteenBytePosition(micro18);
            Console.WriteLine("JSON:");
            var positionJson = Newtonsoft.Json.JsonConvert.SerializeObject(position);
            Console.WriteLine(positionJson);
            Console.WriteLine();

            Console.WriteLine("10 Byte Position");
            Console.WriteLine("----------------");
            byte[] micro10 = GSatMicro.StringToByteArray("08e692636d1f3c5d0007");
            Console.WriteLine("Position Bytes:\n{0}\n", BitConverter.ToString(micro10));
            var tenBytePacketDateUtc = new DateTime(2015, 10, 21, 12, 20, 0, DateTimeKind.Utc);
            var tenBytePosition = GSatMicroPosition.ParseTenBytePosition(micro10, tenBytePacketDateUtc);
            Console.WriteLine("JSON:");
            var tenByteJson = Newtonsoft.Json.JsonConvert.SerializeObject(tenBytePosition);
            Console.WriteLine(tenByteJson);
            Console.WriteLine();

            // Text Message type: 1
            Console.WriteLine("Text Message");
            Console.WriteLine("------------");
            byte[] textMessage = GSatMicro.StringToByteArray("0F737570706F727440677361742E75735468616E6B20796F7520666F7220796F757220696E74657265737420696E2074686520475361744D6963726F21");
            Console.WriteLine("Message Bytes:\n{0}\n", BitConverter.ToString(textMessage));
            var textParsed = GSatMicroTextMessage.ParseTextMessage(textMessage);
            Console.WriteLine("JSON:");
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(textParsed));
            Console.WriteLine();

            // Settings type: 2
            Console.WriteLine("Settings Message");
            Console.WriteLine("----------------");
            byte[] settingsMessageRaw = new byte[] {
                0x03,0x07,0x02,0x00,0x00,0x00,0x14,0x00,0x00,0x00,0x2C,0x01,0x00,0x00,0x3C,0x00,0x00,0x00,0x3C,0x00,0x00,0x00,0x03,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0xFF,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x0F,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x84,0x03,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x02,0x00,0x00,0x00,0x00,0x00,0x00,0x00
            };
            Console.WriteLine("Message Bytes:\n{0}\n", BitConverter.ToString(settingsMessageRaw));
            var settingsParsed = GSatMicroSettings.ParseMessage(settingsMessageRaw);
            Console.WriteLine("JSON:");
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(settingsParsed));
            Console.WriteLine();

            // handling full payload
            var fromMobilePayloads = new List<byte[]>()
            {
                new byte[] { 0xFF,0x34,0xE2,0x1A,0xBB,0x62,0x9C,0xAE,0x5E,0x44,0x8D,0xC7,0x73,0x50,0x7A,0xCD,0xA3,0x33,0x64,0x4B,0x1F,0xD2,0x1E,0x4F,0x85,0x32,0x95,0xE1,0x74,0xCA,0x8E,0x36,0xF5,0xFA,0xE5},
                new byte[] { 0xFF,0x8E,0x1E,0x41,0xED,0x7A,0xA9,0x8F,0xE1,0x39,0x1F,0x8E,0xC7,0x31,0xB2,0xBB,0x96,0xBB,0x0F,0xE1,0xD7,0xF8,0xA9,0x9B,0x95,0xDF,0x82,0x50,0x62,0x89,0x8F,0x0F,0xF8,0x93,0x4C,0x16,0x24,0x5D,0x34 },
            };

            foreach (var fullPayload in fromMobilePayloads)
            {
                var message = GSatMicro.ParseFromMobileMessage(fullPayload, authKey, encryptionKey, DateTime.UtcNow);
                if (message is GSatMicroSettings)
                {
                    Console.WriteLine("Settings Message:\n{0}", Newtonsoft.Json.JsonConvert.SerializeObject((GSatMicroSettings)message));
                }
                else if (message is GSatMicroPosition)
                {
                    Console.WriteLine("Position:\n{0}", Newtonsoft.Json.JsonConvert.SerializeObject((GSatMicroPosition)message));
                }
                else if (message is GSatMicroTextMessage)
                {
                    Console.WriteLine("Text Message:\n{0}", Newtonsoft.Json.JsonConvert.SerializeObject((GSatMicroTextMessage)message));
                }
                Console.WriteLine();
            }
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
           
        }
    }
}

