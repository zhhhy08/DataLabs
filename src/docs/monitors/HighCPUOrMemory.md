# High CPU or Memory Percentage

### Causes
High CPU or memory percentage is relatively unknown. These can often occur due to a deployment having high exception counts, poor memory management and high activity duration, but also it could be related to the kubernetes clusters themselves.

### Mitigation
Restart all services. Please follow the [Restarting Pods Mitigation TSG](../operations/RestartingPods.md).

### Analysis and Assessment
TBD: Memory dump and CPU dump is planned to more clearly assess and diagnose future issues. For now, restarting pods are our best form of mitigation.

Because high memory and high CPU can cause unexpected behavior of the service, we have to assume that all messages that are processing are going to be impacted for that time period.
