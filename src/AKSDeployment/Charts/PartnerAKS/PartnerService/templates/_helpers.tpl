{{- define "otlp_endpoint.mdsd" }}
Endpoint=unix:{{ .partnerSocketPath }}
{{- end }}

{{- define "otlp_endpoint.mdm" }}
Endpoint=unix:{{ .Values.socat.diagnosticEndpoints.mdmCombined.partnerSocketPath }};Account={{ .info.account }};Namespace={{ .info.namespace }}
{{- end }}

{{- define "socat.receiving" }}
UNIX-LISTEN:{{ .value.partnerSocketPath }},fork,reuseaddr,unlink-early,user={{ .partnerConfig.runAsUser | default .partnerApp.runAsUser }},group={{ .partnerConfig.runAsGroup | default .partnerApp.runAsGroup }}
{{- end }}

{{- define "socat.sending" }}
TCP4:$(HOST_IP):{{ .port }}
{{- end }}

{{- define "cachepool_config" }}
{{ $cacheName := .cacheName }}
{{ $readEnabled := .readEnabled | default true | toString }}
{{ $writeEnabled := .writeEnabled | default true | toString }}
{{ $nodeCount := .nodeCount | toString }}
{{ $port := .port | toString }}
{{ $startOffset := .startOffset | toString }}
{{ $nodeSelectionMode := .nodeSelectionMode | default "JumpHash" }}
{{- printf "CacheName=%s;ReadEnabled=%s;WriteEnabled=%s;NodeCount=%s;Port=%s;StartOffset=%s;NodeSelectionMode=%s" $cacheName $readEnabled $writeEnabled $nodeCount $port $startOffset $nodeSelectionMode }}
{{- end }}