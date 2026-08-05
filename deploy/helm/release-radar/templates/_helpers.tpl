{{- define "release-radar.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "release-radar.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name (include "release-radar.name" .) | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}

{{- define "release-radar.labels" -}}
app.kubernetes.io/name: {{ include "release-radar.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version }}
{{- end }}

{{- define "release-radar.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "release-radar.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{- define "release-radar.secretName" -}}
{{- if .Values.secrets.create }}{{ include "release-radar.fullname" . }}{{ else }}{{ .Values.secrets.existingSecret }}{{ end }}
{{- end }}
