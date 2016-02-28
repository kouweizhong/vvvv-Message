using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VVVV.Core.Logging;
using VVVV.Packs.Messaging;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils;

namespace VVVV.Packs.Messaging.Nodes
{
    public abstract class AbstractFormularableNode : ConfigurableNode, IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        [Input("Message Formular", DefaultEnumEntry = "None", EnumName = "VVVV.Packs.Message.Core.Formular", Order = 2)]
        public IDiffSpread<EnumEntry> FFormular;

        [Import]
        protected IHDEHost FHDEHost;

        protected bool SkippedFirst;

        protected override void InitializeWindow()
        {
            // don't call inherited InitializeWindow, so the placeholder pic will disappear
            
            FWindow = new FormularLayoutPanel();
            Controls.Add(FWindow);
        }

        public override void OnImportsSatisfied()
        {
            base.OnImportsSatisfied();

            // base provider of Formulars
            var reg = MessageFormularRegistry.Instance;
            reg.TypeChanged += FormularRemotelyChanged;

            // dummy enum, will be populated from registry
            EnumManager.UpdateEnum(reg.RegistryName, reg.Keys.First(), reg.Keys.ToArray());

            FFormular.Changed += OnSelectFormular;
            ((FormularLayoutPanel)FWindow).Change += OnChangeLayout;

            FConfig.Changed += OnConfig;
        }

        private void OnConfig(IDiffSpread<string> spread)
        {
            if (!SkippedFirst)
            {
                SkippedFirst = true;
                return;
            }


            var formular = new MessageFormular(FConfig[0], MessageFormular.DYNAMIC);

            if (!FFormular.IsAnyInvalid()) formular.Name = FFormular[0]; 
            
            UpdateWindow(formular, true);
        }


        private void OnChangeLayout(object sender, FormularChangedEventArgs e)
        {
            var config = e.Formular.Configuration;
            if (config != FConfig[0]) FConfig[0] = config;
        }


        protected virtual void OnSelectFormular(IDiffSpread<EnumEntry> spread)
        {
            var formularName = FFormular[0].Name;

            if (formularName != MessageFormular.DYNAMIC)
            {
                var formular = new MessageFormular(MessageFormularRegistry.Instance[formularName].FieldDescriptors, formularName);

                foreach (var field in formular.FieldDescriptors) field.IsRequired = false;

                UpdateWindow(formular);

                var newConfig = (FWindow as FormularLayoutPanel).Formular.Configuration;
                if (FConfig[0] != newConfig) FConfig[0] = newConfig;
            }
            else
            {
                (FWindow as FormularLayoutPanel).CanEditFields = true;
                var formular = new MessageFormular(FConfig[0], MessageFormular.DYNAMIC);
                UpdateWindow(formular);
            }

        }


        #region node formular update during runtime
        private void FormularRemotelyChanged(MessageFormularRegistry sender, FormularChangedEventArgs e)
        {
            if (FFormular.IsAnyInvalid()) return;  // strong typing yet undecided

            var used = from formularEntry in FFormular
                       where formularEntry.Name == e.FormularName
                       where formularEntry.Name != MessageFormular.DYNAMIC
                       select true;

            if (used.Any(use => use))
            {
                var layoutPanel = FWindow as FormularLayoutPanel;

                layoutPanel.CanEditFields = e.Formular.IsDynamic;
                layoutPanel.Formular = e.Formular;
            }

            var newConfig = (FWindow as FormularLayoutPanel).Formular.Configuration;
            if (FConfig[0] != newConfig) FConfig[0] = newConfig;
        }

        private void UpdateWindow(MessageFormular formular, bool append = false)
        {
            if (formular == null) return;

            var layoutPanel = FWindow as FormularLayoutPanel;

            if (append) {
                var old = layoutPanel.Formular;
                old.Append(formular, true);
                formular = old;
            }
            
            layoutPanel.Formular = formular;
            layoutPanel.CanEditFields = formular.IsDynamic;
        }
        #endregion node update during runtime

    }
}