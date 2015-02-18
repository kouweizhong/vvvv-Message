﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VVVV.Packs.Messaging;
using VVVV.Packs.Messaging.Nodes;
using VVVV.PluginInterfaces.V2;

namespace VVVV.Nodes.Messaging.Keep
{
    [PluginInfo(Name = "Safe",
        Category = "Message.Keep",
//        Version = "",
        AutoEvaluate = true,
        Help = "Stores Messages according to their address and keeps them updated",
        Author = "velcrome")]
    public class MessageKeepSafe : AbstractMessageKeepNode
    {

        // must override
        protected override void HandleConfigChange(IDiffSpread<string> configSpread)
        {
        }

        public override void Evaluate(int SpreadMax)
        {

            var update = CheckReset();

            foreach (var message in FInput)
            {
                if (message != null && message.Topic != "")
                {
                    MatchOrInsert(message);
//                    if (MatchOrInsert(message) != null) update = true; // unnecessary to carry update here?
                }
            }

            if (UpKeep()) update = true;
            if (update) DumpKeep(Keep.Count);
        }
        
        public Message MatchOrInsert(Message message)
        {
           
            var matched = (from keep in Keep
                           where keep.Topic == message.Topic
                           select keep).ToList();

            if (matched.Count == 0)
            {
                Keep.Add(message); // record message
                return message;
            }
            else
            {
                var found = matched.First(); // found a matching record

                var k = found += message; // copy all attributes from message to matching record
                found.TimeStamp = message.TimeStamp; // update time

                return found;
            }
        }
   
    }
}