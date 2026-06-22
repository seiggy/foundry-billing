#!/bin/sh
set -eu

: "${API_URL:=https://localhost:8080}"

sed "s|__API_URL__|${API_URL}|g" /opt/templates/default.conf.template > /etc/nginx/conf.d/default.conf
