> Azure Resource Graph subscriptions are automatically assigned into one of the below on the creation of the subscription.
> All custom role assignment that is at the management group level should be assigned to these below.  

# Test
* NonProduction-Azure Resource Graph - 0c51acac-60b0-5a0a-5da3-79ee6e9ac255

```
AssignableScopes: [  
"/providers/Microsoft.Management/managementgroups/0c51acac-60b0-5a0a-5da3-79ee6e9ac255"
]
```

# Production
* Production-Azure Resource Graph - 09d6acc6-424b-033e-fa9a-ffc97567edb0
* ProdHobo-Azure Resource Graph - 669fcdb1-f32f-7f25-a45d-135a416d209a
```
AssignableScopes: [   
"/providers/Microsoft.Management/managementgroups/09d6acc6-424b-033e-fa9a-ffc97567edb0",   
"/providers/Microsoft.Management/managementgroups/669fcdb1-f32f-7f25-a45d-135a416d209a"   
]
```

Reference : https://learn.microsoft.com/en-us/azure/governance/management-groups/overview#example-definition
