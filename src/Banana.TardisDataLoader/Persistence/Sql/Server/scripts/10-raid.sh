#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"/..
source ./.env

if [[ -z "${ARRAY_DEV:-}" || ! -b "${ARRAY_DEV:-}" ]]; then
  CANDIDATES=$(lsblk -rno NAME,TYPE,SIZE | awk '$2=="raid5"{print "/dev/"$1" "$3}')
  if [[ -n "$CANDIDATES" ]]; then
    ARRAY_DEV=$(echo "$CANDIDATES" | sort -hk2 | tail -n1 | awk '{print $1}')
  fi
fi

if [[ -z "${ARRAY_DEV:-}" || ! -b "$ARRAY_DEV" ]]; then
  echo "ERROR: ARRAY_DEV not set and autodetect failed." >&2
  exit 1
fi

echo "Using ARRAY_DEV=$ARRAY_DEV"

if ! lsblk -no PARTTYPE "${ARRAY_DEV}" | grep -q . ; then
  parted -s "${ARRAY_DEV}" mklabel gpt
  parted -s "${ARRAY_DEV}" mkpart "${FS_LABEL:-pgdata}" xfs 1MiB 100%
  partprobe "${ARRAY_DEV}"
fi

PART="${ARRAY_DEV}p1"
udevadm settle

SU_KB="${XFS_SU_KB:-128}"
SW="${XFS_SW:-3}"

DETAIL="$(mdadm --detail "$ARRAY_DEV" || true)"
if echo "$DETAIL" | grep -q "Raid Level : raid5"; then
  CHUNK=$(echo "$DETAIL" | awk -F: '/Chunk Size/{gsub(/ /,"",$2); print $2}')
  if [[ "$CHUNK" =~ ^([0-9]+)K$ ]]; then SU_KB="${BASH_REMATCH[1]}"; fi
  N=$(echo "$DETAIL" | awk -F: '/Raid Devices/{gsub(/ /,"",$2); print $2}')
  if [[ "$N" =~ ^[0-9]+$ ]]; then SW="$((N-1))"; fi
fi

echo "mkfs.xfs with su=${SU_KB}k sw=${SW}"

if ! blkid "$PART" | grep -q "TYPE=\"xfs\""; then
  mkfs.xfs -f -L "${FS_LABEL:-pgdata}" -m crc=1,finobt=1 -d "su=${SU_KB}k,sw=${SW}" "$PART"
fi
