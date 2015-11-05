using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GSatMicroLibrary
{
    public class GSatMicroTextMessage : GSatMicroMessage
    {
        public string Text { get; set; }
        public string Destination { get; set; }

        public static GSatMicroTextMessage ParseTextMessage(byte[] payload)
        {
            var destinationLength = payload[0];
            var destinationBytes = payload.Skip(1).Take(destinationLength).ToArray();
            var messageBytes = payload.Skip(destinationLength + 1).ToArray();
            var message = new GSatMicroTextMessage();
            message.Destination = System.Text.Encoding.ASCII.GetString(destinationBytes);
            message.Text = System.Text.Encoding.ASCII.GetString(messageBytes);
            return message;
        }
    }
}
