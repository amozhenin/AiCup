#!/usr/bin/env bash

ts=$(date +%s)
#ts=345

rm local_runner/$ts.txt

./local_runner/codeball2018 \
    --p1 tcp-31002 \
    --p2 tcp-31003 \
    --p1-name 31002 \
    --p2-name 31003 \
    --results-file $ts.txt \
    --seed $ts \
    --duration 500000 \
    --nitro true \
    --team-size 3 \
    --noshow \
    &
echo "LR started"
sleep 1
./cmake-build-release/CodeBall 127.0.0.1 31002 0000000000000000 &
./release/m67 127.0.0.1 31003 0000000000000000 > /dev/null


cat local_runner/$ts.txt
