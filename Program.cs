﻿using System;
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
        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0] == "wmi")
                {
                    WMISetSharing(args[1]);
                }
                else if (args.Length == 2 && args[0] == "hnet")
                {
                    NetSetSharing(args[1]);
                }
                else if (args.Length == 4 && args[0] == "hnet")
                {
                    NetSetSharing(args[1], args[2], args[3]);
                }
                else if (args.Length == 2 && args[0] == "map")
                {
                    NetSetMapping(args[1]);
                }
                else if (args.Length == 3 && args[0] == "map")
                {
                    NetSetMapping(args[1], args[2]);
                }
                else if ((args.Length == 1 || args.Length == 2) && args[0] == "dhcp")
                {
                    WMISetDHCP(args.Length == 1 ? "" : args[1]);
                }
                else if (args.Length == 1 && args[0] == "clean")
                {
                    WMICleanup();
                    NetCleanupSharing();
                }
                else if (args.Length == 1 && args[0] == "cleanmap")
                {
                    NetCleanupMapping();
                }
                else if (args.Length == 2 && args[0] == "sleep")
                {
                    int miliseconds = 0;
                    if (int.TryParse(args[1], out miliseconds))
                        Task.Delay(miliseconds).Wait();
                }
                else
                {
                    ShowUsage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }
            return 0;
        }
        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("natmanager                                           - show usage");
            Console.WriteLine("natmanager sleep <miliseconds>                       - sleep miliseconds");
            Console.WriteLine("natmanager clean                                     - clean up nat using wmi and hnetcfg");
            Console.WriteLine("natmanager cleanmap                                  - clean up nat map using hnetcfg");
            Console.WriteLine("natmanager wmi  <printer interface ip>               - set nat using wmi");
            Console.WriteLine("natmanager hnet <printer interface ip>               - set nat using hnetcfg");
            Console.WriteLine("natmanager hnet <printer interface ip> <public> <private>  - set nat using hnetcfg");
            Console.WriteLine("natmanager map <printer ip>                          - set port forwarding using hnetcfg");
            Console.WriteLine("natmanager map <printer ip> <public>                 - set port forwarding using hnetcfg");
            Console.WriteLine("natmanager dhcp                                      - set dhcp using wmi");
            Console.WriteLine();

            INetSharingManager SharingManager = new NetSharingManager();
            Console.WriteLine("Sharing {0}, SharingManager.EnumEveryConnection:", SharingManager.SharingInstalled ? "installed" : "not installed");
            Console.WriteLine($"{"Name",-30} | {"DeviceName",-40} | Status");
            Console.WriteLine($"{new string('-', 30)} | {new string('-', 40)} | {new string('-', 20)}");
            foreach (INetConnection n in SharingManager.EnumEveryConnection)
            {
                var p = SharingManager.NetConnectionProps[n];
                if (p.MediaType == tagNETCON_MEDIATYPE.NCM_LAN)
                    Console.WriteLine($"{p.Name,-30} | {p.DeviceName,-40} | {p.Status.ToString().Substring(4)}");
            }
            Console.WriteLine();

            var srchr = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus=2");
            Console.WriteLine("SELECT Win32_NetworkAdapter WHERE NetConnectionStatus=2:");
            Console.WriteLine($"{"DeviceName",-40} | {"Description",-40} | AdapterType");
            Console.WriteLine($"{new string('-', 40)} | {new string('-', 40)} | {new string('-', 20)}");
            foreach (ManagementObject entry in srchr.Get())
            {
                Console.WriteLine($"{entry["Name"],-40} | {entry["Description"],-40} | {entry["AdapterType"]}");
            }
            Console.WriteLine();
        }
        static void NetCleanupMapping()
        {
            INetSharingManager SharingManager = new NetSharingManager();
            foreach (INetConnection n in SharingManager.EnumEveryConnection)
            {
                var p = SharingManager.NetConnectionProps[n];
                var c = SharingManager.INetSharingConfigurationForINetConnection[n];

                if (p.MediaType == tagNETCON_MEDIATYPE.NCM_LAN)
                {
                    if (!p.DeviceName.ToLower().Contains("posnet") && !p.DeviceName.ToLower().Contains("hyper-v") && p.Status == tagNETCON_STATUS.NCS_CONNECTED)
                    {
                        foreach (INetSharingPortMapping mapping in c.EnumPortMappings[tagSHARINGCONNECTION_ENUM_FLAGS.ICSSC_DEFAULT])
                        {
                            if (mapping.Properties.Name == "FSP")
                            {
                                mapping.Disable();
                                mapping.Delete();
                                break;
                            }
                        }
                    }
                }
            }

        }
        static void NetCleanupSharing()
        {
            INetSharingManager SharingManager = new NetSharingManager();
            foreach (INetConnection n in SharingManager.EnumEveryConnection)
            {
                var c = SharingManager.INetSharingConfigurationForINetConnection[n];

                if (c.SharingEnabled)
                    c.DisableSharing();
            }
        }
        static void NetSetSharing(string posnetip, string publicInterface = "", string privateInterface = "")
        {
            INetSharingManager SharingManager = new NetSharingManager();
            INetSharingConfiguration posnetsc = null;
            string posnetguid = null;
            INetConnectionProps posnetp = null;
            INetSharingConfiguration lansc = null;
            INetConnectionProps lanp = null;

            foreach (INetConnection n in SharingManager.EnumEveryConnection)
            {
                var p = SharingManager.NetConnectionProps[n];
                var c = SharingManager.INetSharingConfigurationForINetConnection[n];
                if (p.MediaType == tagNETCON_MEDIATYPE.NCM_LAN)
                {
                    if (!string.IsNullOrEmpty(publicInterface) && !string.IsNullOrEmpty(privateInterface))
                    {
                        if (p.Name == publicInterface)
                        {
                            lansc = c;
                            lanp = p;
                        }
                        if (p.Name == privateInterface)
                        {
                            posnetsc = c;
                            posnetguid = p.Guid;
                            posnetp = p;
                        }
                    }
                    else if (p.DeviceName.ToLower().Contains("posnet"))
                    {
                        posnetsc = c;
                        posnetguid = p.Guid;
                        posnetp = p;
                    }
                    else if (!p.DeviceName.ToLower().Contains("hyper-v") && p.Status == tagNETCON_STATUS.NCS_CONNECTED)
                    {
                        lansc = c;
                        lanp = p;
                    }
                }
            }

            if (posnetsc == null)
                throw new ApplicationException("ERROR: Cannot find printer interface");
            if (lansc == null)
                throw new ApplicationException("ERROR: Cannot find lan interface");

            lansc.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PUBLIC);
            posnetsc.EnableSharing(tagSHARINGCONNECTIONTYPE.ICSSHARINGTYPE_PRIVATE);

            WMISetIP(posnetguid, posnetip, "255.255.255.0");


            Console.WriteLine("Sharing on " + lanp.Name + " (public) enabled=" + lansc.SharingEnabled + " , " + posnetp.Name + " (private) enabled=" + posnetsc.SharingEnabled);
        }
        static void NetSetMapping(string printerip, string publicInterface = "")
        {
            INetSharingManager SharingManager = new NetSharingManager();
            INetSharingConfiguration lansc = null;
            INetConnectionProps lanp = null;
            string languid = null;

            foreach (INetConnection n in SharingManager.EnumEveryConnection)
            {
                var p = SharingManager.NetConnectionProps[n];
                var c = SharingManager.INetSharingConfigurationForINetConnection[n];
                if (p.MediaType == tagNETCON_MEDIATYPE.NCM_LAN)
                {
                    if (!string.IsNullOrEmpty(publicInterface))
                    {
                        if (p.Name == publicInterface)
                        {
                            lansc = c;
                            lanp = p;
                            languid = p.Guid;
                        }
                    }
                    else if (!p.DeviceName.ToLower().Contains("posnet") && !p.DeviceName.ToLower().Contains("hyper-v") && p.Status == tagNETCON_STATUS.NCS_CONNECTED)
                    {
                        lansc = c;
                        lanp = p;
                        languid = p.Guid;
                    }
                }
            }

            if (lansc == null)
                throw new ApplicationException("ERROR: Cannot find lan interface");


            var m = lansc.AddPortMapping("FSP", 17, 2121, 2121, 0, printerip, tagICS_TARGETTYPE.ICSTT_IPADDRESS);
            m.Enable();


            Console.WriteLine("Mapping on " + lanp.Name + " (public) enabled=" + lansc.SharingEnabled);


        }
        public static void WMISetDHCP(string searchdescription)
        {
            var options = new PutOptions();
            options.Type = PutType.UpdateOnly;
            var srchr = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE AdapterType='Ethernet 802.3' and NetConnectionStatus=2");
            string lan = null;
            string languid = null;
            foreach (ManagementObject entry in srchr.Get())
            {
                string description = entry["Description"].ToString();
                if (description.ToLower().Contains("hyper-v"))
                    continue;

                if (description.ToLower().Contains("posnet"))
                    continue;

                if (!string.IsNullOrEmpty(searchdescription) && !description.ToLower().Contains(searchdescription))
                    continue;

                lan = description;
                languid = entry["Guid"].ToString();
            }
            if (languid == null)
                throw new ApplicationException($"ERROR: Cannot find lan interface {searchdescription}");

            WMISetDHCPGuid(languid);

            Console.WriteLine("DHCP on " + lan + " (public) enabled");
        }

        public static void WMISetDHCPGuid(string guid)
        {
            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();

            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"] && objMO["SettingID"].Equals(guid))
                {
                    var res = objMO.InvokeMethod("EnableDHCP", null);
                    break;
                }
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
                    newIP["SubnetMask"] = new string[] { mask };

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
        public static void WMISetSharing(string posnetip)
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
            if (posnet == null)
                throw new ApplicationException("ERROR: Cannot find printer interface");
            if (lan == null)
                throw new ApplicationException("ERROR: Cannot find lan interface");
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
