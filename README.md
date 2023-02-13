# NatManager
```
Usage:
natmanager                                           - show usage
natmanager sleep <miliseconds>                       - sleep miliseconds
natmanager clean                                     - clean up nat using wmi and hnetcfg
natmanager cleanmap                                  - clean up nat map using hnetcfg
natmanager wmi  <printer interface ip>               - set nat using wmi
natmanager hnet <printer interface ip> <printer ip>  - set nat using hnetcfg
natmanager map <printer ip>                          - set port forwarding using hnetcfg
natmanager dhcp                                      - set dhcp on public interface using wmi
```