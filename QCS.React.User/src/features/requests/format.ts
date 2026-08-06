/**
 * Formatting helpers, kept out of the component files: a module that exports both components and
 * plain functions breaks React Fast Refresh, which oxlint flags. Mirrors QRS's
 * src/features/quotations/format.ts so the two portals read the same.
 */

/**
 * Sizes on the documents panels. KB below a megabyte, MB above it — a 4 MB file reported as
 * "4096 KB" is technically right and useless at a glance. Empty for a missing or zero size: the
 * generated FinalPdf row carries no FileSize, and "0 KB" would read as a broken file.
 */
export function formatFileSize(bytes?: number): string {
  if (!bytes || bytes <= 0) return ''
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
