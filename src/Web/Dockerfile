# syntax=docker/dockerfile:1
#
# Angular 20 SPA: build with Node, serve the static bundle with nginx.
# Build context is src/Web (set in docker-compose). The production build (default configuration)
# emits to dist/web/browser and bakes apiBase=http://localhost:8080 from environment.ts.

# ---- build stage -------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

# Install deps from the lockfile first for layer caching.
COPY package.json package-lock.json ./
RUN npm ci

# Build the production bundle.
COPY . .
RUN npm run build

# ---- runtime stage -----------------------------------------------------------
FROM nginx:alpine AS runtime
# Static SPA assets. @angular/build:application emits the browser bundle under dist/web/browser.
COPY --from=build /app/dist/web/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
