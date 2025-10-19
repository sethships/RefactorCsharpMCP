#!/bin/bash

#
# Cache Stability Test Script
#
# Runs cache-related tests multiple times to verify stability and detect
# intermittent failures due to concurrency issues.
#
# Usage: ./test-cache-stability.sh [--iterations N]
#
# Default: 10 iterations
#

set -e

# Parse arguments
ITERATIONS=10
while [[ $# -gt 0 ]]; do
  case $1 in
    --iterations)
      ITERATIONS="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [--iterations N]"
      exit 1
      ;;
  esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

header() { echo -e "\n${CYAN}==== $1 ====${NC}"; }
success() { echo -e "${GREEN}✓ $1${NC}"; }
error() { echo -e "${RED}✗ $1${NC}"; }
warning() { echo -e "${YELLOW}⚠ $1${NC}"; }
info() { echo -e "${GRAY}  $1${NC}"; }

header "Cache Stability Test - $ITERATIONS Iterations"

# Test filter for cache-related tests
FILTER="FullyQualifiedName~ReferenceAssemblyCache|FullyQualifiedName~FrameworkTestFixture|FullyQualifiedName~TupleReturnConverter|FullyQualifiedName~NullableReferenceTypeStripper"

echo "Start time: $(date)"
echo "Iterations: $ITERATIONS"
echo "Test filter: Cache-related tests"
echo ""

# Statistics tracking
PASS_COUNT=0
FAIL_COUNT=0
declare -a DURATIONS=()
TOTAL_DURATION=0

# Run iterations
for i in $(seq 1 $ITERATIONS); do
  echo -ne "Run $i/$ITERATIONS... "

  START=$(date +%s)

  # Run tests and capture output
  if dotnet test --no-build --verbosity quiet --filter "$FILTER" > /tmp/cache-stability-run-$i.txt 2>&1; then
    END=$(date +%s)
    DURATION=$((END - START))
    DURATIONS+=($DURATION)
    TOTAL_DURATION=$((TOTAL_DURATION + DURATION))

    echo -e "${GREEN}✓ PASSED${NC} (${DURATION}s)"
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    END=$(date +%s)
    DURATION=$((END - START))
    DURATIONS+=($DURATION)
    TOTAL_DURATION=$((TOTAL_DURATION + DURATION))

    echo -e "${RED}✗ FAILED${NC} (${DURATION}s)"
    FAIL_COUNT=$((FAIL_COUNT + 1))
    error "See /tmp/cache-stability-run-$i.txt for details"
  fi
done

echo ""

# Calculate statistics
AVG_DURATION=$((TOTAL_DURATION / ITERATIONS))

# Find min/max
MIN_DURATION=${DURATIONS[0]}
MAX_DURATION=${DURATIONS[0]}
for duration in "${DURATIONS[@]}"; do
  if [ $duration -lt $MIN_DURATION ]; then
    MIN_DURATION=$duration
  fi
  if [ $duration -gt $MAX_DURATION ]; then
    MAX_DURATION=$duration
  fi
done

# Calculate standard deviation (simple approximation)
SUM_SQUARED_DIFF=0
for duration in "${DURATIONS[@]}"; do
  DIFF=$((duration - AVG_DURATION))
  SQUARED_DIFF=$((DIFF * DIFF))
  SUM_SQUARED_DIFF=$((SUM_SQUARED_DIFF + SQUARED_DIFF))
done
VARIANCE=$((SUM_SQUARED_DIFF / ITERATIONS))
STDDEV=$(echo "sqrt($VARIANCE)" | bc)

# Calculate pass rate
PASS_RATE=$((PASS_COUNT * 100 / ITERATIONS))

# Display summary
header "Summary"
echo ""
printf "%-20s %s\n" "Total Runs:" "$ITERATIONS"
printf "%-20s ${GREEN}%s${NC}\n" "Passed:" "$PASS_COUNT"
if [ $FAIL_COUNT -gt 0 ]; then
  printf "%-20s ${RED}%s${NC}\n" "Failed:" "$FAIL_COUNT"
else
  printf "%-20s %s\n" "Failed:" "$FAIL_COUNT"
fi
printf "%-20s " "Pass Rate:"
if [ $PASS_RATE -eq 100 ]; then
  echo -e "${GREEN}${PASS_RATE}%${NC}"
else
  echo -e "${RED}${PASS_RATE}%${NC}"
fi
echo ""
printf "%-20s %ss\n" "Average Time:" "$AVG_DURATION"
printf "%-20s %ss\n" "Min Time:" "$MIN_DURATION"
printf "%-20s %ss\n" "Max Time:" "$MAX_DURATION"
printf "%-20s %ss\n" "Std Deviation:" "$STDDEV"
printf "%-20s %ss\n" "Total Time:" "$TOTAL_DURATION"
echo ""

# Final verdict
if [ $FAIL_COUNT -eq 0 ]; then
  success "All $ITERATIONS runs successful - cache concurrency stable! ✅"
  echo ""
  exit 0
else
  error "Cache stability issues detected - $FAIL_COUNT/$ITERATIONS runs failed"
  warning "Review logs in /tmp/cache-stability-run-*.txt for details"
  echo ""
  exit 1
fi
