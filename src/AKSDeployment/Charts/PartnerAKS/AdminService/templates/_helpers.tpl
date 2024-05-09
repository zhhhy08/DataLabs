{{- define "otlp_endpoint.mdsd" }}
Endpoint=unix:{{ .applicationEndpoint }}
{{- end }}

{{- define "otlp_endpoint.mdm" }}
Endpoint=unix:{{ .Values.mdm.applicationEndpoint }};Account={{ .info.account }};Namespace={{ .info.namespace }}
{{- end }}
