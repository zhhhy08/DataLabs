{{- define "otlp_endpoint.mdsd" }}
Endpoint=unix:{{ .applicationEndpoint }}
{{- end }}

{{- define "otlp_endpoint.mdm" }}
Endpoint=unix:{{ .Values.mdm.applicationEndpoint }};Account={{ .info.account }};Namespace={{ .info.namespace }}
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