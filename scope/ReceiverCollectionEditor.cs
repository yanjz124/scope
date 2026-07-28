using System;
using System.ComponentModel.Design;

namespace DGScope.Receivers
{
    public class ReceiverCollectionEditor : CollectionEditor
    {
        public ReceiverCollectionEditor(Type type) : base(type)
        {
        }

        protected override Type[] CreateNewItemTypes()
        {
            return ReceiverPlugins.DiscoverReceiverTypes();
        }
    }


}
