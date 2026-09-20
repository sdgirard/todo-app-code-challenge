import { useState, type ChangeEvent, type FormEvent } from 'react'
import { Form } from 'react-router'
import type { TodoResponse } from '../api/generated/models'
import { toDateTimeLocal } from '../lib/dateTimeLocal'

const TITLE_MAX_LENGTH = 200
const DESCRIPTION_MAX_LENGTH = 2000

export interface TodoFormValues {
  title: string
  description: string
  dueDate: string
}

interface TodoFormProps {
  intent: 'add' | 'edit'
  defaultValues?: TodoFormValues
  fieldErrors?: Record<string, string[]>
  submitting?: boolean
  /** Edit mode only: the todo being edited, used to pre-fill fields and as the dirty-tracking baseline. */
  todo?: TodoResponse
  /** Edit mode only: discards in-progress edits and returns to the read-only view without submitting. */
  onCancel?: () => void
}

const DEFAULT_VALUES: TodoFormValues = { title: '', description: '', dueDate: '' }

const valuesFromTodo = (todo: TodoResponse): TodoFormValues => ({
  title: todo.title,
  description: todo.description ?? '',
  dueDate: toDateTimeLocal(todo.dueDate),
})

export const TodoForm = ({
  intent,
  defaultValues,
  fieldErrors = {},
  submitting = false,
  todo,
  onCancel,
}: TodoFormProps) => {
  const [clientError, setClientError] = useState<string | undefined>(undefined)

  const originalValues = intent === 'edit' && todo ? valuesFromTodo(todo) : DEFAULT_VALUES
  const initialValues = defaultValues ?? originalValues

  // Edit mode only: current field values, tracked for the dirty comparison that
  // gates the Save button. Add mode doesn't need this — there's no "original" to diff against.
  const [currentValues, setCurrentValues] = useState<TodoFormValues>(initialValues)

  const handleFieldChange = (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = event.currentTarget
    setCurrentValues((prev) => ({ ...prev, [name]: value }))
  }

  const isDirty =
    intent === 'edit' &&
    (currentValues.title !== originalValues.title ||
      // "" and null both mean "no description" — not a difference worth enabling Save for.
      (currentValues.description || null) !== (originalValues.description || null) ||
      currentValues.dueDate !== originalValues.dueDate)

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    const form = event.currentTarget
    const formData = new FormData(form)
    const title = (formData.get('title') as string | null)?.trim() ?? ''
    const description = formData.get('description') as string | null

    if (!title) {
      event.preventDefault()
      setClientError('Title is required.')
      return
    }

    if (title.length > TITLE_MAX_LENGTH) {
      event.preventDefault()
      setClientError(`Title must be ${TITLE_MAX_LENGTH} characters or fewer.`)
      return
    }

    if ((description?.length ?? 0) > DESCRIPTION_MAX_LENGTH) {
      event.preventDefault()
      setClientError(`Description must be ${DESCRIPTION_MAX_LENGTH} characters or fewer.`)
      return
    }

    setClientError(undefined)
  }

  const titleError = clientError ?? fieldErrors.Title?.[0]
  const descriptionError = fieldErrors.Description?.[0]
  const dueDateError = fieldErrors.DueDate?.[0]

  const title = currentValues.title.trim()
  const isValid =
    title.length > 0 &&
    title.length <= TITLE_MAX_LENGTH &&
    currentValues.description.length <= DESCRIPTION_MAX_LENGTH

  const saveDisabled = submitting || !isDirty || !isValid

  return (
    <Form method="post" onSubmit={handleSubmit} className="mb-6 flex flex-col gap-3">
      <input type="hidden" name="intent" value={intent} />
      {intent === 'edit' && todo && (
        <input type="hidden" name="currentIsCompleted" value={String(todo.isCompleted)} />
      )}

      <div>
        <label htmlFor="title" className="mb-1 block text-sm font-medium text-gray-700">
          Title
        </label>
        <input
          id="title"
          name="title"
          type="text"
          value={currentValues.title}
          onChange={handleFieldChange}
          maxLength={TITLE_MAX_LENGTH}
          placeholder="What needs doing?"
          className="w-full rounded border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
        />
        {titleError && <p className="mt-1 text-sm text-red-600">{titleError}</p>}
      </div>

      <div>
        <label htmlFor="description" className="mb-1 block text-sm font-medium text-gray-700">
          Description
        </label>
        <textarea
          id="description"
          name="description"
          value={currentValues.description}
          onChange={handleFieldChange}
          maxLength={DESCRIPTION_MAX_LENGTH}
          placeholder="Longer explanation (optional)"
          className="w-full rounded border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
        />
        {descriptionError && <p className="mt-1 text-sm text-red-600">{descriptionError}</p>}
      </div>

      <div>
        <label htmlFor="dueDate" className="mb-1 block text-sm font-medium text-gray-700">
          Due date
        </label>
        <input
          id="dueDate"
          name="dueDate"
          type="datetime-local"
          value={currentValues.dueDate}
          onChange={handleFieldChange}
          className="w-full rounded border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
        />
        {dueDateError && <p className="mt-1 text-sm text-red-600">{dueDateError}</p>}
      </div>

      <div className="flex gap-2">
        {intent === 'edit' ? (
          <>
            <button
              type="submit"
              disabled={saveDisabled}
              className="self-start rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {submitting ? 'Saving…' : 'Save'}
            </button>
            <button
              type="button"
              onClick={onCancel}
              disabled={submitting}
              className="self-start rounded border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 disabled:opacity-50"
            >
              Cancel
            </button>
          </>
        ) : (
          <button
            type="submit"
            disabled={submitting}
            className="self-start rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
          >
            {submitting ? 'Adding…' : 'Add to-do'}
          </button>
        )}
      </div>
    </Form>
  )
}
