using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DGScope.Receivers
{
    public class ReceiverCollectionEditor : CollectionEditor
    {
        private List<Type> types = new List<Type>();

        public ReceiverCollectionEditor(Type type) : base(type)
        {
            LoadReceivers();
        }

        private void LoadReceivers()
        {
            // Look beside the executable, not in the working directory. The facility
            // config is chosen through an OpenFileDialog, which leaves the process
            // working directory pointing at the profile folder - where there are no
            // plugins - so scanning it left the "Add" list empty and no receiver could
            // be added at all.
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                dir = Environment.CurrentDirectory;

            foreach (var dll in new DirectoryInfo(dir).GetFiles("DGScope.*.dll"))
            {
                Type[] assemblyTypes;
                try
                {
                    assemblyTypes = Assembly.LoadFrom(dll.FullName).GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Keep whatever did load; one broken plugin must not hide the rest.
                    assemblyTypes = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in assemblyTypes)
                {
                    if (typeof(Receiver).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                        types.Add(type);
                }
            }
        }


        protected override Type[] CreateNewItemTypes()
        {
            return types.ToArray();
        }
    }


}
