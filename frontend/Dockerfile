# syntax=docker/dockerfile:1

# ---- build stage: compile the Angular app ----
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies first so this layer is cached until package*.json changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration production

# ---- runtime stage: static files served by nginx ----
FROM nginx:1.27-alpine AS runtime

# The nginx image renders /etc/nginx/templates/*.template into /etc/nginx/conf.d at start-up,
# replacing ${BACKEND_URL}. NGINX_ENTRYPOINT_LOCAL_RESOLVERS exposes the container's DNS server
# as ${NGINX_LOCAL_RESOLVERS}, which the template needs for run-time upstream lookup.
ENV BACKEND_URL=http://backend:8080 \
    NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1

COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=15s --timeout=3s --retries=3 \
  CMD wget -qO- http://127.0.0.1/ >/dev/null || exit 1
