FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf.template /opt/templates/default.conf.template
COPY docker-entrypoint.sh /docker-entrypoint.d/40-generate-nginx-config.sh

RUN chmod +x /docker-entrypoint.d/40-generate-nginx-config.sh

EXPOSE 80
ENV API_URL=https://localhost:8080

CMD ["nginx", "-g", "daemon off;"]
