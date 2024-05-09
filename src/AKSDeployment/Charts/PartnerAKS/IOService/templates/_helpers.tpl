{{- define "otlp_endpoint.mdsd" }}
Endpoint=unix:{{ .applicationEndpoint }}
{{- end }}

{{- define "otlp_endpoint.mdm" }}
Endpoint=unix:{{ .Values.mdm.applicationEndpoint }};Account={{ .info.account }};Namespace={{ .info.namespace }}
{{- end }}

{{- define "partner.singleresponseresourcesroutingconfig" }}
{{ $routingConfig := "" }}
  {{- range $pk, $pv := .partnerConfig.pods }}
    {{- range $ck, $cv := $pv.containers }} 
      {{ if not (empty $cv.singleResponseResourcesMatchTypes) }}
        {{ $port := $cv.port | toString  }}
        {{ $partnerChannelName := printf "%s-%s" $pv.serviceName  $port }}
        {{ $singleResponseResourcesMatchTypes := $cv.singleResponseResourcesMatchTypes | fromJson }}
        {{ $resourceTypes := default "" (index $singleResponseResourcesMatchTypes "resourceTypes") }}
        {{ $eventTypes := default "" (index $singleResponseResourcesMatchTypes "eventTypes") }}
        {{ $partnerChannelAddress := printf "dns:///%s.%s.svc.cluster.local:%s" $pv.serviceName $.partnerNameSpace $port  }}
        {{ $routingConfig = printf "%s{\"resourceTypes\":\"%s\",\"eventTypes\":\"%s\",\"partnerChannelAddress\":\"%s\", \"partnerChannelName\":\"%s\"};" $routingConfig $resourceTypes $eventTypes $partnerChannelAddress $partnerChannelName }}
      {{- end }}
    {{- end }}
  {{- end }}
  {{- printf "%s" $routingConfig }}
{{- end }}

{{- define "partner.multiresponseresourcesroutingconfig" }}
{{ $multiResponseRoutingConfig := "" }}
  {{- range $pk, $pv := .partnerConfig.pods }}
    {{- range $ck, $cv := $pv.containers }} 
      {{ if not (empty $cv.multiResponseResourcesMatchTypes) }}
        {{ $port := $cv.port | toString  }}
        {{ $partnerChannelName := printf "%s-%s" $pv.serviceName $port }}
        {{ $multiResponseResourcesMatchTypes := $cv.multiResponseResourcesMatchTypes | fromJson }}
        {{ $resourceTypes := default "" (index $multiResponseResourcesMatchTypes "resourceTypes") }}
        {{ $eventTypes := default "" (index $multiResponseResourcesMatchTypes "eventTypes") }}
        {{ $partnerChannelAddress := printf "dns:///%s.%s.svc.cluster.local:%s" $pv.serviceName $.partnerNameSpace $port  }}
        {{ $multiResponseRoutingConfig = printf "%s{\"resourceTypes\":\"%s\",\"eventTypes\":\"%s\",\"partnerChannelAddress\":\"%s\", \"partnerChannelName\":\"%s\"};" $multiResponseRoutingConfig $resourceTypes $eventTypes $partnerChannelAddress $partnerChannelName }}
      {{- end }}
    {{- end }}
  {{- end }}
{{- printf "%s" $multiResponseRoutingConfig }}
{{- end }}

{{- define "partner.io.concurrency" }}
{{ $concurrencyConfig := "" }}
  {{- range $pk, $pv := .partnerConfig.pods }}
    {{- range $ck, $cv := $pv.containers }} 
        {{ $concurrency := $cv.concurrency | default 0 | toString }}
        {{ $port := $cv.port | toString  }}
        {{ $partnerChannelName := printf "%s-%s" $pv.serviceName  $port }}
        {{ $concurrencyConfig = printf "%s%s:%s;" $concurrencyConfig $partnerChannelName $concurrency }}
    {{- end }}
  {{- end }}
{{- printf "%s" $concurrencyConfig }}
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
