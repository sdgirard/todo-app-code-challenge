interface ConfirmDialogProps {
  open: boolean
  message: string
  onConfirm: () => void
  onCancel: () => void
  disabled?: boolean
}

export const ConfirmDialog = ({ open, message, onConfirm, onCancel, disabled }: ConfirmDialogProps) => {
  if (!open) return null

  return (
    <div className="fixed inset-0 flex items-center justify-center bg-black/50">
      <div className="flex flex-col gap-4 rounded bg-white p-6 shadow-lg">
        <p className="text-sm text-gray-900">{message}</p>

        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={disabled}
            className="rounded border border-gray-300 px-3 py-1 text-sm text-gray-700 disabled:opacity-50"
          >
            No
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={disabled}
            className="rounded bg-red-600 px-3 py-1 text-sm text-white disabled:opacity-50"
          >
            Yes
          </button>
        </div>
      </div>
    </div>
  )
}
