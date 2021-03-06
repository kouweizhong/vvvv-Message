using System;
using System.Collections.Generic;
using System.Linq;
using VVVV.PluginInterfaces.V2;

namespace VVVV.Packs.Messaging.Nodes
{
    public abstract class TypableMessageKeepNode : AbstractMessageKeepNode
    {
        public const string TOPIC = "Topic";
        public IDiffSpread<EnumEntry> FUseAsID = null;
        private string EnumName;

        public override void OnImportsSatisfied()
        {
            base.OnImportsSatisfied();
            CreateEnumPin("Use as ID", new string[] { TOPIC });

            FormularUpdate += (sender, formular) => FillEnum(formular.FieldNames.ToArray());

        }

        public void CreateEnumPin(string pinName, IEnumerable<string> entries)
        {
            EnumName = "Enum_" + this.GetHashCode().ToString();

            EnumManager.UpdateEnum(EnumName, entries.First(), entries.ToArray());

            var attr = new InputAttribute(pinName);
            attr.Order = 2;
            attr.AutoValidate = true;

            attr.EnumName = EnumName;

            Type pinType = typeof(IDiffSpread<EnumEntry>);
            var pin = FIOFactory.CreateIOContainer(pinType, attr);
            FUseAsID = (IDiffSpread<EnumEntry>)(pin.RawIOObject);
        }

        private void FillEnum(IEnumerable<string> fields)
        {
            var entries = Enumerable.Repeat(TOPIC, 1).Concat(fields);
            EnumManager.UpdateEnum(EnumName, entries.First(), entries.ToArray());
        }



    }
}