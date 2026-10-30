# Maintain the supported PostgreSQL 16 data format without retaining the
# privilege-switching helper from the upstream image.
#
# The official entrypoint only invokes gosu when running as root.
# Running under the image's existing postgres account avoids that branch.
# Verify fresh init and existing-volume access before replacing a running image.
FROM postgres:16.15-alpine3.24
ARG SOURCE_REVISION=unverified
LABEL org.opencontainers.image.revision=$SOURCE_REVISION

USER root
RUN set -eux; \
    test -f /usr/local/bin/gosu; \
    rm /usr/local/bin/gosu /usr/local/bin/su-exec; \
    apk upgrade --no-cache; \
    test ! -e /usr/local/bin/gosu; \
    test ! -e /usr/local/bin/su-exec

USER postgres
