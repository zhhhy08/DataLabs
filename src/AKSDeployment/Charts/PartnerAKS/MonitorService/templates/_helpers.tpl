{{- define "socat.monitorservice.receiving" }}
TCP-LISTEN:{{ .port }},reuseaddr,fork
{{- end }}

{{- define "socat.monitorservice.sending" }}
UNIX-CONNECT:{{ .monitorSocketPath }}
{{- end }}
