# Kubernetes cluster provider

The Kubernetes cluster provider uses the Kubernetes API to watch cluster nodes status.

When the pod starts, the provider will assign a set of labels to the pod:

| Label | Value |
| :---- | :---- |
| cluster.proto.actor/cluster | cluster name |
| cluster.proto.actor/port | node port |
| cluster.proto.actor/kinds | known node kinds |
| cluster.proto.actor/status-value | the status value |

As you notice, one labels contains the node port, but there's no label for the node address.
The provider will use the pod IP address for the node address.

The `status-value` label gets updated if the node gets a new status.

The provider watches all the pods in its namespace. It matches all new, updated and terminated pods with the same cluster name and updates the members list accordingly.
Only the pods that are ready will be announced as the cluster members, so it is crucial to ensure that the readiness probe is set up correctly and represents the actual readiness of the member.

The provider watches all the pods in the namespace and updates the running pod. It uses the `default` service account and therefore the service account should get some extra rights. Use the following manifest for the service account to make it work:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: pod-read-role
rules:
- apiGroups: ["*"]
  resources: ["*"]
  verbs: ["get", "list", "watch", "update", "patch"]
---
kind: RoleBinding
apiVersion: rbac.authorization.k8s.io/v1
metadata:
  name: pod-read-rolebinding
subjects:
- kind: ServiceAccount
  name: default
roleRef:
  kind: Role
  name: pod-read-role
  apiGroup: ""
```

