#!/bin/sh
# Entrypoint of the WaveMP image: chooses the user that runs WaveMP, then starts it ("$@").
#
# Started as another user than root (user: in compose, --user, the app user of TrueNAS), WaveMP
# runs unchanged as that user; PUID and PGID are ignored and nothing is changed.
#
# Started as root (the default of the image), it
# - hands the top level of the data folder (DATA_PATH, /data by default) to the user PUID and the
#   group PGID (1654 by default, the user app of the image), so platforms that create the folders
#   of their mounts as root work without further steps; folders and files inside are left as they
#   are,
# - keeps the supplementary groups of the container (group_add, --group-add) and adds the groups
#   of the render devices under /dev/dri, so hardware transcoding keeps working,
# - and runs WaveMP as PUID and PGID with su-exec. PUID=0 keeps running it as root.
set -eu

log() {
    echo "wavemp-entrypoint: $*" >&2
}

if [ "$(id -u)" != "0" ]; then
    exec "$@"
fi

uid="${PUID:-1654}"
gid="${PGID:-1654}"
case "$uid" in
    '' | *[!0-9]*)
        log "PUID must be a numeric user id, not '$uid'."
        exit 1
        ;;
esac
case "$gid" in
    '' | *[!0-9]*)
        log "PGID must be a numeric group id, not '$gid'."
        exit 1
        ;;
esac

if [ "$uid" = "0" ]; then
    log "PUID is 0, WaveMP runs as root."
    exec "$@"
fi

# Only the top level: WaveMP creates its folders inside itself, and a recursive change could take
# long on a large cache and would change files the user may have set up on purpose.
data="${DATA_PATH:-/data}"
if [ ! -e "$data" ]; then
    mkdir -p "$data" || log "Could not create the data folder $data."
fi
if [ -d "$data" ] && [ "$(stat -c '%u:%g' "$data")" != "$uid:$gid" ]; then
    if ! chown "$uid:$gid" "$data"; then
        log "Could not hand the data folder $data to user $uid and group $gid; WaveMP may not be able to write it."
    fi
fi

# Supplementary groups: those of this process that root does not get from /etc/group (added with
# group_add), and the groups of the render devices.
root_groups=" $(id -G root) "
groups=""
add_group() {
    if [ "$1" != "$gid" ] && [ "$1" != "0" ]; then
        case " $groups " in
            *" $1 "*) ;;
            *) groups="${groups:+$groups }$1" ;;
        esac
    fi
}
for group in $(id -G); do
    case "$root_groups" in
        *" $group "*) ;;
        *) add_group "$group" ;;
    esac
done
for device in /dev/dri/renderD* /dev/dri/card*; do
    if [ -c "$device" ]; then
        add_group "$(stat -c '%g' "$device")"
    fi
done

if [ -z "$groups" ]; then
    log "WaveMP runs as user $uid and group $gid."
    exec su-exec "$uid:$gid" "$@"
fi

# su-exec sets the supplementary groups only from /etc/passwd and /etc/group, so the user gets an
# entry of its own (wavemp) with one group entry per supplementary group. Entries of an earlier
# start of the same container are replaced. Duplicate ids next to existing entries are allowed.
home="$(getent passwd "$uid" | grep -v '^wavemp:' | head -n 1 | cut -d: -f6 || true)"
# A subshell, because set -e does not apply inside the condition of an if.
if (
    sed -i '/^wavemp:/d' /etc/passwd || exit 1
    sed -i '/^wavemp-[0-9]*:/d' /etc/group || exit 1
    echo "wavemp:x:$uid:$gid::${home:-/}:/sbin/nologin" >>/etc/passwd || exit 1
    for group in $groups; do
        echo "wavemp-$group:x:$group:wavemp" >>/etc/group || exit 1
    done
) 2>/dev/null; then
    log "WaveMP runs as user $uid and group $gid with the supplementary groups $groups."
    exec su-exec wavemp "$@"
fi

log "Could not write /etc/passwd and /etc/group; WaveMP runs as user $uid and group $gid" \
    "without the supplementary groups $groups."
exec su-exec "$uid:$gid" "$@"
