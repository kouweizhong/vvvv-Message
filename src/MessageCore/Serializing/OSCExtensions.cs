﻿using System.IO;
using VVVV.Utils.OSC;

namespace VVVV.Packs.Messaging.Serializing
{
    using Time = VVVV.Packs.Time.Time;

    public static class OSCExtensions
    {

        public static Stream ToOSC(this Message message, bool extendedMode = false)
        {
            OSCBundle bundle = new OSCBundle(message.TimeStamp, extendedMode);
            foreach (string name in message.Fields)
            {
                string oscAddress = "";

                foreach (string part in message.Topic.Split('.'))
                {
                    if (part.Trim() != "") oscAddress += "/" + part;
                }

                foreach (string part in name.Split('.'))
                {
                    if (part.Trim() != "") oscAddress += "/" + part;
                }

                OSCMessage m = new OSCMessage(oscAddress, extendedMode);
                Bin bl = message[name];
                for (int i = 0; i < bl.Count; i++) m.Append(bl[i]);
                bundle.Append(m);
            }
            return new MemoryStream(bundle.BinaryData); // packs implicitly
        }

        public static Message FromOSC(Stream stream, bool extendedMode = false, string messagePrefix = "", int contractAddress = 1)
        {
            Message message = new Message();

            MemoryStream ms = new MemoryStream();
            stream.Position = 0;
            stream.CopyTo(ms);
            byte[] bytes = ms.ToArray();
//            int start = 0;
//            OSCBundle bundle = OSCBundle.Unpack(bytes, ref start, (int)stream.Length, extendedMode);

            var pack = OSCPacket.Unpack(bytes, extendedMode);

            OSCBundle bundle;
            if (pack.IsBundle())
            {
                bundle = (OSCBundle)pack;
                message.TimeStamp = bundle.getTimeStamp();
            } else {
                bundle = new OSCBundle(extendedMode);
                var m = (OSCMessage)pack;
                bundle.Append(m);
                message.TimeStamp = Time.CurrentTime();
            }


            foreach (OSCMessage m in bundle.Values)
            {
                //                  Todo: mixing of types in a singular message is not allowed right now! however, many uses of osc do mix values

                Bin bin = BinFactory.New(m.Values[0].GetType());
                bin.AssignFrom(m.Values); // does not clone implicitly

                string oldAddress = m.Address;
                while (oldAddress.StartsWith("/")) oldAddress = oldAddress.Substring(1);
                while (oldAddress.EndsWith("/")) oldAddress = oldAddress.Substring(0, oldAddress.Length - 1);

                string[] address = oldAddress.Split('/');

                contractAddress = address.Length > contractAddress ? contractAddress : address.Length - ((messagePrefix.Trim() == "") ? 1 : 0);
                string attribName = "";
                for (int i = address.Length - contractAddress; i < address.Length; i++)
                {
                    attribName += ".";
                    attribName += address[i];
                    address[i] = "";
                }
                attribName = attribName.Substring(1);

                string messageAddress = "";
                foreach (string part in address)
                {
                    if (part.Trim() != "") messageAddress += "." + part;
                }
                if (messagePrefix.Trim() == "") message.Topic = messageAddress.Substring(1);
                else message.Topic = messagePrefix + messageAddress;

                message[attribName] = bin;
            }

            return message;
        }
    }
}
