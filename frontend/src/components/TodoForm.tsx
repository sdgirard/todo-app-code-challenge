import { useState, type FormEvent } from 'react'
import { Form } from 'react-router'

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
}

const DEFAULT_VALUES: TodoFormValues = { title: '', description: '', dueDate: '' }

export const TodoForm = ({
  intent,
  defaultValues = DEFAULT_VALUES,
  fieldErrors = {},
  submitting = false,
}: TodoFormProps) => {
  const [clientError, setClientError] = useState<string | undefined>(undefined)

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    const form = event.currentTarget
    const title = (new FormData(form).get('title') as string | null)?.trim() ?? ''

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

    setClientError(undefined)
  }

  const titleError = clientError ?? fieldErrors.Title?.[0]
  const descriptionError = fieldErrors.Description?.[0]
  const dueDateError = fieldErrors.DueDate?.[0]

  return (
    <Form method="post" onSubmit={handleSubmit} className="mb-6 flex flex-col gap-3">
      <input type="hidden" name="intent" value={intent} />

      <div>
        <label htmlFor="title" className="mb-1 block text-sm font-medium text-gray-700">
          Title
        </label>
        <input
          id="title"
          name="title"
          type="text"
          defaultValue={defaultValues.title}
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
          defaultValue={defaultValues.description}
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
          defaultValue={defaultValues.dueDate}
          className="w-full rounded border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
        />
        {dueDateError && <p className="mt-1 text-sm text-red-600">{dueDateError}</p>}
      </div>

      <button
        type="submit"
        disabled={submitting}
        className="self-start rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
      >
        {submitting ? 'Adding…' : 'Add to-do'}
      </button>
    </Form>
  )
}
