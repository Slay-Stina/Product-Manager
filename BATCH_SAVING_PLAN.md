# Batch Saving Implementation Plan

## Current Issue
Products are saved one at a time with `SaveChangesAsync()` after each product, resulting in:
- Many database round trips
- Poor performance with large product sets
- Transaction overhead for each save

## Proposed Solution

### Option 1: Batch Size Approach (Recommended)
- Accumulate products in memory
- Save in batches of N products (e.g., 50)
- Balance between memory usage and performance

### Option 2: Single Transaction
- Collect all products first
- Save everything in one transaction at the end
- Best performance but higher memory usage

## Implementation

### Changes needed:
1. **Add batch tracking fields** to ProductCrawlerService
2. **Create batch save method** that handles bulk inserts/updates
3. **Modify crawling flow** to accumulate products instead of immediate saves
4. **Call flush/save** at strategic points (end of crawl, batch size reached)

### Benefits:
- **10-50x faster** for large product sets
- Reduced database load
- Better transaction handling
- Can implement retry logic for failed batches

### Trade-offs:
- Slightly more memory usage
- Need to handle partial failures
- Must ensure final flush happens

## Recommendation
Use batch size of 50 products as it provides:
- Good performance improvement
- Manageable memory footprint
- Easy error recovery
