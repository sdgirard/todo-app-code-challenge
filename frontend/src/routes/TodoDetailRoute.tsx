import { useEffect, useRef, useState } from 'react'
import { Link, useActionData, useLoaderData, useNavigation, useSubmit } from 'react-router'
import { TodoDetail } from '../components/TodoDetail'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { TodoForm } from '../components/TodoForm'
import type { TodoResponse } from '../api/generated/models'
import type { TodoDetailActionData } from './TodoDetailRoute.action'

export const TodoDetailRoute = () => {
  const todo = useLoaderData() as TodoResponse
  const actionData = useActionData() as TodoDetailActionData | undefined
  const navigation = useNavigation()
  const submit = useSubmit()
  const [showConfirm, setShowConfirm] = useState(false)
  const [isEditing, setIsEditing] = useState(false)

  const submitting =
    navigation.state === 'submitting' && navigation.formMethod?.toLowerCase() === 'post'

  // Only the edit form itself submits while isEditing is true (Delete/toggle
  // are unmounted in edit mode — see the JSX below), so any non-submitting
  // navigation state observed while still in edit mode means that edit
  // submission is no longer in flight — it went submitting -> loading (while
  // the loader re-runs with the saved values) -> idle, per React Router's
  // <Form>-based submission lifecycle. wasEverNonIdle distinguishes that from
  // "just entered edit mode," which starts idle without ever having left it.
  // A finished submission with no field errors succeeded, so drop back to the
  // read-only view — synchronizing local UI state with the router's
  // navigation lifecycle is exactly what an effect is for here.
  const wasEverNonIdle = useRef(false)
  useEffect(() => {
    if (navigation.state !== 'idle') {
      wasEverNonIdle.current = true
      return
    }

    if (!isEditing || !wasEverNonIdle.current) return
    wasEverNonIdle.current = false

    const hasFieldErrors = !!actionData?.fieldErrors && Object.keys(actionData.fieldErrors).length > 0
    if (!hasFieldErrors) setIsEditing(false)
  }, [navigation.state, actionData, isEditing])

  const handleConfirmDelete = () => {
    const formData = new FormData()
    formData.set('intent', 'delete')
    submit(formData, { method: 'post' })
  }

  const handleToggleComplete = () => {
    const formData = new FormData()
    formData.set('intent', 'toggleComplete')
    formData.set('isCompleted', String(!todo.isCompleted))
    submit(formData, { method: 'post' })
  }

  return (
    <main className="mx-auto max-w-md p-6">
      {!isEditing && (
        <Link to="/" className="mb-4 inline-block text-sm text-gray-500 hover:underline">
          &larr; Back to list
        </Link>
      )}

      {isEditing ? (
        <TodoForm
          intent="edit"
          todo={todo}
          onCancel={() => setIsEditing(false)}
          fieldErrors={actionData?.fieldErrors}
          submitting={submitting}
        />
      ) : (
        <>
          <TodoDetail todo={todo} />

          <div className="mt-4 flex gap-2">
            <button
              type="button"
              onClick={handleToggleComplete}
              disabled={submitting}
              className="rounded bg-gray-700 px-3 py-1 text-sm text-white disabled:opacity-50"
            >
              {todo.isCompleted ? 'Incomplete' : 'Complete'}
            </button>

            <button
              type="button"
              onClick={() => setIsEditing(true)}
              disabled={submitting}
              className="rounded bg-blue-600 px-3 py-1 text-sm text-white disabled:opacity-50"
            >
              Edit
            </button>

            <button
              type="button"
              onClick={() => setShowConfirm(true)}
              disabled={submitting}
              className="rounded bg-red-600 px-3 py-1 text-sm text-white disabled:opacity-50"
            >
              Delete
            </button>
          </div>
        </>
      )}

      <ConfirmDialog
        open={showConfirm}
        message="Delete this to-do?"
        onConfirm={handleConfirmDelete}
        onCancel={() => setShowConfirm(false)}
        disabled={submitting}
      />
    </main>
  )
}
