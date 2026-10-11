FROM node:20-alpine AS build
WORKDIR /app

COPY SAPS.DDMS.Frontend/package*.json ./
RUN npm ci

COPY SAPS.DDMS.Frontend/ .
RUN npm run build

FROM nginx:1.27-alpine AS runtime

COPY SAPS.DDMS.Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/SAPS.DDMS.Frontend/browser /usr/share/nginx/html

EXPOSE 80
