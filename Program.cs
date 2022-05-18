using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using NETCONLib;
using System.Management;
using System.Diagnostics;

namespace NatManager
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                ShowUsage();
                return;
            }

            if (args[0] == "wmi")
            {
                WMICleanup();
                WMISetSharing(args[1], "");
            }
            else if (args[0] == "hnet")
            {
                NetCleanupSetSharing(args[1], args[2]);
            }
            else                
            {
                ShowUsage();
            }
        }
        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("natmanager                                           - show usage");
            Console.WriteLine("natmanager wmi <printer interface ip>                - set nat using wmi");
            Console.WriteLine("natmanager hnet <printer interface ip> <printer ip>  - set nat and port forwarding using hnetcfg");
            Console.WriteLine();

            INetSharingManager SharingManager = new NetSharingManager();
            Console.WriteLine("Sharing {0}, interfaces:", SharingManager.SharingInstalled ? "installed" : "not installed");
            Console.WriteLine($"{"Name",-30} | {"DeviceName",-40} | Status");
            Console.WriteLine($"{new string('-', 30)} | {new string('-', 40)} | {new string('-', 20)}");
            foreach (INetConnection n in SharingManager.EnumEveryConnection)
            {
                var p = SharingManager.NetConnectionProps[n];
                if (p.MediaType == tagNETCON_MEDIATYPE.NCM_LAN)
                    Console.WriteLine($"{p.Name,-30} | {p.DeviceName,-40} | {p.Status.ToString().Substring(4)}");
            }
        }
        static void NetCleanupSetSharing(string posnetip, string printerip)
        { 
            INetSharingManager SharingManager = new NetSharingManager();
            INetSharingConfiguration posnet = null;
            string posnetguid = null;
            INetSharingConfiguration lan = null;

            foreach (INetConnection n in SharingManager.EnumEveryConnection)
            {
                var p = SharingManager.NetConnectionProps[n];
                var c = SharingManager.INetSharingConfigurationForINetConnection[n];
                if (c.SharingEnabled)
                    c.DisableSharing();
                if (p.MediaType == tagNETCON_MEDIATYPE.NCM_LAN)
                {
                    if (p.DeviceName.ToLower().Contains("posnet"))
                    {
                        posnet = c;
                        posnetguid = p.Guid;
                    }
                    else if (!p.DeviceName.ToLower().Contains("hyper-v") && p.Status == tagNETCON_STATUS.NCS_CONNECTED)
                    {
                        lan = c;
                    }
                }
            }
            if (posnet != null && lan != null)
            {
                posnet.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PRIVATE);
                lan.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PUBLIC);
                WMISetIP(posnetguid, posnetip, "255.255.255.0");

                var m = lan.AddPortMapping("FSP", 17 , 2121, 2121, 0, printerip, tagICS_TARGETTYPE.ICSTT_IPADDRESS);
                m.Enable();
            }
            
        }

        public static void WMISetIP(string guid, string ip, string mask)
        {
            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();

            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"] && objMO["SettingID"].Equals(guid))
                {
                    ManagementBaseObject setIP;
                    ManagementBaseObject newIP = objMO.GetMethodParameters("EnableStatic");

                    newIP["IPAddress"] = new string[] { ip };
                    newIP["SubnetMask"] = new string[] { mask};

                    setIP = objMO.InvokeMethod("EnableStatic", newIP, null);
                    break;
                }
            }
        }

        public static void WMICleanup()
        {
            var options = new PutOptions();
            options.Type = PutType.UpdateOnly;

            var srchr = new ManagementObjectSearcher("root\\Microsoft\\HomeNet", "SELECT * FROM HNet_ConnectionProperties WHERE IsIcsPrivate=true or IsIcsPublic=true");
            foreach (ManagementObject entry in srchr.Get())
            {
                entry["IsIcsPrivate"] = false;
                entry["IsIcsPublic"] = false;
                entry.Put(options);
            }
        }
        public static void WMISetSharing(string posnetip, string printerip)
        {
            var options = new PutOptions();
            options.Type = PutType.UpdateOnly;
            var srchr = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE AdapterType='Ethernet 802.3' and NetConnectionStatus=2");
            string posnet = null;
            string lan = null;
            foreach (ManagementObject entry in srchr.Get())
            {
                string description = entry["Description"].ToString();
                if (description.ToLower().Contains("hyper-v"))
                    continue;

                string guid = entry["Guid"].ToString();
                Console.WriteLine($"{guid} {description,-30} ");

                if (description.ToLower().Contains("posnet"))
                {
                    posnet = guid;
                    foreach (ManagementObject subentry in entry.GetRelated("Win32_NetworkAdapterConfiguration"))
                    {
                        var parameters = subentry.GetMethodParameters("EnableStatic");
                        parameters["IPAddress"] = new string[] { posnetip };
                        parameters["SubnetMask"] = new string[] { "255.255.255.0" };
                        subentry.InvokeMethod("EnableStatic", parameters, null);
                        break;
                    }
                }
                else 
                {
                    lan = guid;
                }
            }
            srchr = new ManagementObjectSearcher("root\\Microsoft\\HomeNet", "SELECT * FROM HNet_ConnectionProperties"); // WHERE IsIcsPrivate=true or IsIcsPublic=true");
            foreach (ManagementObject entry in srchr.Get())
            {
                string c = entry["Connection"].ToString();
                if (c.Contains(posnet))
                {
                    entry["IsIcsPrivate"] = true;
                    entry.Put(options);
                }
                else if (c.Contains(lan))
                {
                    entry["IsIcsPublic"] = true;
                    entry.Put(options);
                }
            }

        }

    }
}
