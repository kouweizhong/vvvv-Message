using System.Collections.Generic;
using System.Linq;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils;

namespace VVVV.Packs.Messaging.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "Create", AutoEvaluate = true, Category = "Message.Keep", Help = "Joins a permanent Message from custom dynamic pins", Version = "Formular", Tags = "Dynamic, Bin", Author = "velcrome")]
    #endregion PluginInfo
    public class MessageKeepCreateNode : DynamicPinsNode
    {
#pragma warning disable 649, 169
        [Input("New", IsBang = true, Order = 0)]
        public IDiffSpread<bool> FNew;

        [Input("Topic", DefaultString = "State", Order = 1)]
        public IDiffSpread<string> FTopic;

        [Input("Reset", IsBang = true, Order = int.MaxValue - 2)]
        public IDiffSpread<bool> FReset;

        [Input("Count", IsSingle = true, DefaultValue = 1, Order = int.MaxValue - 1)]
        public IDiffSpread<int> FSpreadCount;

        [Output("Output", AutoFlush = false)]
        public Pin<Message> FOutput;

        [Output("Changed Message", Order = int.MaxValue - 2, AutoFlush = false, Visibility = PinVisibility.Hidden)]
        public Pin<int> FChangeOut;

        [Output("Message Diff", Order = int.MaxValue - 1, AutoFlush = false)]
        public Pin<Message> FChangeDataOut;

        [Output("Internal Count", Order = int.MaxValue, AutoFlush = false)]
        public ISpread<int> FCountOut;
#pragma warning restore

        protected MessageKeep Keep = new MessageKeep(); // same as in AbstractStorageNode. 

        protected virtual bool CheckReset()
        {
            if ((!FReset.IsAnyInvalid() && FReset[0]) || FConfig.IsChanged)
            {
                Keep.Clear();
                return true;
            }
            return false;

        }

        protected virtual bool UpKeep(bool enforceFull = false)
        {
            FChangeOut.SliceCount = 0;
            FChangeDataOut.SliceCount = 0;

            if (Keep.IsChanged)
            {
                if (FChangeDataOut.IsConnected || FChangeOut.IsConnected)
                {
                    IEnumerable<Message> changes;
                    if (!FChangeOut.IsConnected && !enforceFull)
                    {
                        changes = Keep.Sync();
                    }
                    else // more expensive to get the indices as well
                    {
                        IEnumerable<int> indexes;
                        changes = Keep.Sync(out indexes);

                        foreach (var index in indexes) FChangeOut.Add(index);
                        FChangeOut.Flush();
                    }
                    FChangeDataOut.AssignFrom(changes);
                    FChangeDataOut.Flush();
                }
                else Keep.Sync(true);


                FCountOut[0] = Keep.Count;
                FCountOut.Flush();

                return true;

            }
            return false;
        }

        protected void DumpKeep(int SpreadMax)
        {
            FOutput.SliceCount = SpreadMax;

            for (int i = 0; i < SpreadMax; i++)
            {
                var message = Keep[i];
                FOutput[i] = message;

            }

            FOutput.Flush();
        }

        protected override IOAttribute DefinePin(FormularFieldDescriptor configuration)
        {
            var attr = new InputAttribute(configuration.Name); 
            attr.BinVisibility = PinVisibility.Hidden;
            attr.BinSize = configuration.DefaultSize;
            attr.Order = DynPinCount;
            attr.BinOrder = DynPinCount + 1;
            attr.CheckIfChanged = true;
            //attr.AutoValidate = false;  // need to sync all pins manually. Don't forget to Flush()
            return attr;
        }

        public override void Evaluate(int SpreadMax)
        {
            var oldCount = Keep.Count;
            SpreadMax = FSpreadCount[0];
            SpreadMax = SpreadMax < 0 ? 0 : SpreadMax; // safeguard against negative binsizes

//          Reset?
            var update = CheckReset() || FTopic.IsChanged;

//          remove superfluous entries
            if (SpreadMax < oldCount) 
            {
                update = true;
                Keep.RemoveRange(SpreadMax, oldCount - SpreadMax);
            }

//          add new entries
            for (int i = Keep.Count; i < SpreadMax; i++)
            {
                update = true; // new entry in Keep will require data
                Keep.Add(new Message(Formular));
            }

//          see if any Message needs to refreshed
            for (int i = 0; i < SpreadMax; i++)
                if (FNew[i])
                {
                    update = true;
                    Keep[i] = new Message();
                }

            var newData = FPins.Any(x => x.Value.ToISpread().IsChanged); // changed pins

            if (!newData && !update) return;

            //// only now get the data from upstream
            //if (update) SyncPins();
            
            // ...and start filling messages
            int messageIndex = 0;
            foreach (var message in Keep)
            {
               CopyFromPins(message, messageIndex, true);
               messageIndex++;
            }

            if (UpKeep()) update = true;

            if (update) DumpKeep(FSpreadCount[0]);
        }



    }
}