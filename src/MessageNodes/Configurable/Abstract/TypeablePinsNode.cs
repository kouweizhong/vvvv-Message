﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using VVVV.PluginInterfaces.V2;
using VVVV.Utils;
using VVVV.Core.Logging;
using System.Reflection;


namespace VVVV.Packs.Messaging.Nodes
{
    public abstract class DynamicPinsNode : AbstractFormularableNode
    {
        #region fields & pins
        protected const string Tags = "Formular";

        [Input("Verbose", Visibility = PinVisibility.OnlyInspector, IsSingle = true, DefaultBoolean = false, Order = 2)]
        public ISpread<bool> FDevMode;

        [Import()]
        protected IIOFactory FIOFactory;

        protected Dictionary<string, IIOContainer> FPins = new Dictionary<string, IIOContainer>();
        protected MessageFormular Formular = new MessageFormular("");

        protected bool RemovePinsFirst;

        protected int DynPinCount = 5;
        #endregion fields & pins

        #region pin management

        protected bool SyncPins()
        {
            bool changed = false;
            foreach (string name in FPins.Keys)
            {
                var pin = FPins[name].ToISpread();
                pin.Sync();
                if (pin.IsChanged) changed = true;
            }
            return changed;
        }

        protected bool CopyFromPins(Message message, int index, bool checkPinForChange = false)
        {
            var hasCopied = false;

            foreach (string name in FPins.Keys)
            {
                // don't change if pin data still the same
                if (!checkPinForChange || FPins[name].ToISpread().IsChanged)
                {
                    var pinSpread = FPins[name].ToISpread();
                    if (!pinSpread.IsAnyInvalid())
                    {
                        var bin = pinSpread[index] as IEnumerable;

                        // don't change if pin data equals the message data
                        if (!message.Fields.Contains(name) || !message[name].Equals(bin))
                        {
                            message.AssignFrom(name, bin);
                            hasCopied = true;
                        }  
                    }
                }
            }
            return hasCopied;

        }

        protected IIOContainer CreatePin(FormularFieldDescriptor field)
        {
            IOAttribute attr = SetPinAttributes(field); // each implementation of DynamicPinsNode must create its own InputAttribute or OutputAttribute (

            Type pinType = typeof(ISpread<>).MakeGenericType((typeof(ISpread<>)).MakeGenericType(field.Type)); // the Pin is always a binsized one
            var pin = FPins[field.Name] = FIOFactory.CreateIOContainer(pinType, attr);

            DynPinCount += 2; // total pincount. always add two to account for data pin and binsize pin

            return pin;
        }

        private void RetryConfig(object sender, EventArgs e)
        {
            this.HandleConfigChange(FConfig);
        }

        protected bool CheckForConnection(IIOContainer pinContainer)
        {
            var binContainer = pinContainer.RawIOObject.GetType().GetField("FStream", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(pinContainer.RawIOObject);
            var container = binContainer.GetType().GetField("FDataContainer", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance).GetValue(binContainer) as IIOContainer;
            return container.GetPluginIO().IsConnected;
        }

        protected override void HandleConfigChange(IDiffSpread<string> configSpread)
        {
            var newFormular = new MessageFormular(configSpread[0]);

            // pin removals
            var danger = from pinName in FPins.Keys
                         where !newFormular.FieldNames.Contains(pinName)
                         where CheckForConnection(FPins[pinName]) // first frame pin will not be initialized
                         select pinName;

            // type changes - removal and recreate new
            danger.Concat(
                            from desc in Formular.FieldDescriptors
                            where FPins.Keys.Contains(desc.Name)
                            where newFormular[desc.Name].Type != desc.Type
                            where CheckForConnection(FPins[desc.Name]) // first frame pin will not be initialized
                            select desc.Name
                         );
            // ignore changes to binsize.



            if (danger.Count() > 0)
            {
                RemovePinsFirst = true;
                FHDEHost.MainLoop.OnPrepareGraph += RetryConfig;
                // throw exceptions, until danger count is zero during evaluate
                // this will highlight afflicted nodes in red until all endangered links are removed by hand
                return;
            }
            else
            {
                RemovePinsFirst = false;
                FHDEHost.MainLoop.OnPrepareGraph -= RetryConfig;
                Formular = newFormular;
            }

            List<string> invalidPins = FPins.Keys.ToList();
            foreach (string field in newFormular.FieldNames)
            {
                if (FPins.ContainsKey(field) && FPins[field] != null)
                {
                    invalidPins.Remove(field);

                    if (Formular.FieldNames.Contains(field))
                    {
                        // same name, but types don't match
                        // todo: in fact eg float does match double here...
                        if (Formular[field].Type != newFormular[field].Type)
                        {
                            FPins[field].Dispose();
                            FPins[field] = CreatePin(newFormular[field]);
                        }
                    }
                    else
                    {
                        // key is in FPins, but no type defined. should never happen
                        FLogger.Log(LogType.Debug, "Internal Fault in Pin Layout detected. Override with "+newFormular.ToString());
                        FPins[field] = CreatePin(newFormular[field]);
                    }
                }
                else
                {
                    FPins[field] = CreatePin(newFormular[field]);
                }
             }
            
            // cleanup
            foreach (string name in invalidPins)
            {
                FPins[name].Dispose();
                FPins.Remove(name);
            }

            //// reorder - does not work right now, sdk offers only read-only access
            //var names = formular.FieldNames.ToArray();
            //for (int i = 0; i < formular.FieldNames.Count; i++)
            //{
            //    var name = names[i];
            //    var pin = FPins[name].GetPluginIO();
            //    pin.Order = i * 2 + 5;
            //}

        }
        #endregion pin management

        #region abstract methods
        protected abstract IOAttribute SetPinAttributes(FormularFieldDescriptor config);
        #endregion abstract methods
    }

}
