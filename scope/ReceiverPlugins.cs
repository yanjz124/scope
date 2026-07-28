using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DGScope.Receivers
{
    /// <summary>
    /// Marks a receiver the scope should provide out of the box. One instance is added
    /// automatically when a facility config contains none, so the receiver does not have
    /// to be added by hand before it can be used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DefaultReceiverAttribute : Attribute
    {
    }

    /// <summary>
    /// Finds receiver implementations shipped alongside the executable.
    /// </summary>
    public static class ReceiverPlugins
    {
        private static Type[] discovered;

        /// <summary>
        /// Every concrete Receiver type in the DGScope assemblies next to the executable.
        /// Deliberately not the working directory: the facility config is chosen with a
        /// file dialog, which moves the working directory to the profile folder, where
        /// there are no plugins at all.
        /// </summary>
        public static Type[] DiscoverReceiverTypes()
        {
            if (discovered != null)
                return discovered;

            string dir = AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                dir = Environment.CurrentDirectory;

            var found = new List<Type>();
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
                        found.Add(type);
                }
            }

            discovered = found.ToArray();
            return discovered;
        }

        /// <summary>
        /// Adds one instance of each [DefaultReceiver] type the list does not already
        /// contain. An existing entry is left exactly as it is, so a receiver the user
        /// has turned off stays off rather than being re-enabled on every launch.
        /// </summary>
        /// <returns>The receivers that were added.</returns>
        public static List<Receiver> EnsureDefaults(IList<Receiver> receivers)
        {
            var added = new List<Receiver>();
            if (receivers == null)
                return added;

            foreach (var type in DiscoverReceiverTypes())
            {
                if (!Attribute.IsDefined(type, typeof(DefaultReceiverAttribute)))
                    continue;
                if (receivers.Any(r => r != null && r.GetType() == type))
                    continue;

                try
                {
                    var receiver = (Receiver)Activator.CreateInstance(type);
                    receiver.Enabled = true;
                    receivers.Add(receiver);
                    added.Add(receiver);
                }
                catch
                {
                    // A plugin that cannot be constructed is simply not offered.
                }
            }
            return added;
        }
    }
}
