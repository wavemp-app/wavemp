#!/usr/bin/env bash
# Tests the entrypoint of the WaveMP image: which user and groups run WaveMP and what happens to
# the data folder, started as root (the default), with PUID and PGID, with group_add, with render
# devices and started as another user (--user). Instead of WaveMP, each container runs a probe that
# prints its user, groups, home folder, the owner of /data and whether it can write /data.
#
# Usage: test-image-user.sh [image]   (default wavemp:dev; needs docker)
set -euo pipefail

# Keeps Git Bash on Windows from rewriting the container paths.
export MSYS_NO_PATHCONV=1

image="${1:-wavemp:dev}"
prefix="wavemp-user-test-$$"
failures=0

# The probe runs as the command of the entrypoint.
read -r -d '' probe <<'EOF' || true
if touch /data/.probe 2>/dev/null; then write=yes; else write=no; fi
sub=$(stat -c %u:%g /data/backups 2>/dev/null || echo none)
# id -G names the primary group twice when it is also a supplementary group; listed once here.
groups=$(id -G | tr ' ' '\n' | awk '!seen[$0]++' | paste -s -d, -)
echo "uid=$(id -u) gid=$(id -g) groups=$groups home=$HOME data=$(stat -c %u:%g /data) backups=$sub write=$write"
EOF

cleanup() {
    docker rm --force "$prefix-restart" >/dev/null 2>&1 || true
    docker volume rm --force "$prefix-data" >/dev/null 2>&1 || true
}
trap cleanup EXIT

# expect <name> <expected output> <docker run arguments...>: runs the probe and compares its line.
expect() {
    local name="$1" expected="$2"
    shift 2
    local actual
    if ! actual=$(docker run --rm "$@" 2>/dev/null); then
        echo "FAIL $name: the container failed"
        failures=$((failures + 1))
        return
    fi
    if [[ "$actual" == "$expected" ]]; then
        echo "ok   $name"
    else
        echo "FAIL $name"
        echo "     expected: $expected"
        echo "     actual:   $actual"
        failures=$((failures + 1))
    fi
}

entrypoint=(--entrypoint wavemp-entrypoint)
# A tmpfs (mode 755) belongs to root, like the folders that Unraid, ZimaOS and CasaOS create for their mounts.
rootdata=(--mount type=tmpfs,destination=/data,tmpfs-mode=0755)

expect "root start runs as 1654 and hands /data to it" \
    "uid=1654 gid=1654 groups=1654 home=/home/app data=1654:1654 backups=none write=yes" \
    "${rootdata[@]}" "${entrypoint[@]}" "$image" sh -c "$probe"

expect "PUID and PGID choose user and group (Unraid 99:100)" \
    "uid=99 gid=100 groups=100 home=/ data=99:100 backups=none write=yes" \
    -e PUID=99 -e PGID=100 "${rootdata[@]}" "${entrypoint[@]}" "$image" sh -c "$probe"

expect "empty PUID and PGID fall back to 1654" \
    "uid=1654 gid=1654 groups=1654 home=/home/app data=1654:1654 backups=none write=yes" \
    -e PUID= -e PGID= "${rootdata[@]}" "${entrypoint[@]}" "$image" sh -c "$probe"

expect "PUID=0 keeps root" \
    "uid=0 gid=0 groups=0,1,2,3,4,6,10,11,20,26,27 home=/root data=0:0 backups=none write=yes" \
    -e PUID=0 "${rootdata[@]}" "${entrypoint[@]}" "$image" sh -c "$probe"

expect "--user runs as that user and changes nothing (TrueNAS 568:568)" \
    "uid=568 gid=568 groups=568 home=/ data=0:0 backups=none write=no" \
    --user 568:568 -e PUID=99 -e PGID=100 "${rootdata[@]}" "${entrypoint[@]}" "$image" sh -c "$probe"

expect "group_add is kept after the switch" \
    "uid=99 gid=100 groups=100,993 home=/ data=99:100 backups=none write=yes" \
    --group-add 993 -e PUID=99 -e PGID=100 "${rootdata[@]}" "${entrypoint[@]}" "$image" sh -c "$probe"

expect "group_add is kept for the default user" \
    "uid=1654 gid=1654 groups=1654,993 home=/home/app data=1654:1654 backups=none write=yes" \
    --group-add 993 "${rootdata[@]}" "${entrypoint[@]}" "$image" sh -c "$probe"

# A render device with group 44 (video on Debian and Ubuntu), created before the entrypoint runs.
expect "the group of a render device is added" \
    "uid=99 gid=100 groups=100,44 home=/ data=99:100 backups=none write=yes" \
    -e PUID=99 -e PGID=100 "${rootdata[@]}" --entrypoint sh "$image" -c \
    'mkdir -p /dev/dri && mknod /dev/dri/renderD128 c 1 3 && chgrp 44 /dev/dri/renderD128 && exec wavemp-entrypoint sh -c "$0"' "$probe"

# Only the top level of /data changes owner: a folder that root created inside stays root's.
docker volume create "$prefix-data" >/dev/null
docker run --rm -v "$prefix-data:/data" --entrypoint sh "$image" -c 'chown 0:0 /data && mkdir /data/backups'
expect "only the top level of /data changes owner" \
    "uid=99 gid=100 groups=100 home=/ data=99:100 backups=0:0 write=yes" \
    -e PUID=99 -e PGID=100 -v "$prefix-data:/data" "${entrypoint[@]}" "$image" sh -c "$probe"

# A restarted container keeps its files, including the user entry of the first start.
restart_probe='echo "$(id -u) $(id -G | tr " " ",") $(grep -c "^wavemp:" /etc/passwd) $(grep -c "^wavemp-993:" /etc/group)"'
docker create --name "$prefix-restart" --group-add 993 -e PUID=99 -e PGID=100 \
    "${entrypoint[@]}" "$image" sh -c "$restart_probe" >/dev/null
first=$(docker start --attach "$prefix-restart" 2>/dev/null)
second=$(docker start --attach "$prefix-restart" 2>/dev/null)
if [[ "$first" == "99 100,993 1 1" && "$second" == "$first" ]]; then
    echo "ok   a restart replaces the user entry of the first start"
else
    echo "FAIL a restart replaces the user entry of the first start: '$first', then '$second'"
    failures=$((failures + 1))
fi

for invalid in "PUID=abc" "PGID=-1"; do
    if docker run --rm -e "$invalid" "${entrypoint[@]}" "$image" true >/dev/null 2>&1; then
        echo "FAIL $invalid is refused"
        failures=$((failures + 1))
    else
        echo "ok   $invalid is refused"
    fi
done

if ((failures > 0)); then
    echo "$failures test(s) failed."
    exit 1
fi
echo "All tests of the image user passed."
