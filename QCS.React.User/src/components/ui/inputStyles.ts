const inputBase = 'rounded-sm border border-border-subtle bg-surface-panel text-body focus:border-accent focus:outline-2 focus:outline-offset-1 focus:outline-accent disabled:cursor-not-allowed disabled:opacity-50'

export function appInputClassName(size: 'sm' | 'md' = 'md', className = '') {
  const sizeClass = size === 'sm' ? 'h-8 px-2' : 'h-9 px-2'
  return `${inputBase} ${sizeClass} ${className}`
}

export function appTextareaClassName(className = '') {
  return `${inputBase} p-2 ${className}`
}