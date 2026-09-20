import { redirect, type ActionFunctionArgs } from 'react-router'
import { deleteTodo, updateCompletionStatus, updateTodo } from '../api/generated/todos/todos'
import { getFieldErrors, getMessage } from '../lib/problemDetails'

export interface TodoDetailActionData {
  fieldErrors: Record<string, string[]>
  message?: string
}

export const todoDetailAction = async ({
  params,
  request,
}: ActionFunctionArgs): Promise<Response | TodoDetailActionData | null> => {
  const id = params.id as string
  const formData = await request.formData()
  const intent = formData.get('intent')

  if (intent === 'delete') {
    // Both documented outcomes (204 deleted, 404 already gone) satisfy the
    // user's intent that this todo no longer exist — see spec's Action section.
    await deleteTodo(id)
    return redirect('/')
  }

  if (intent === 'toggleComplete') {
    const isCompleted = formData.get('isCompleted') === 'true'
    const result = await updateCompletionStatus(id, { isCompleted })
    const status: number = result.status

    if (status === 200) {
      return null
    }

    if (status === 404) {
      return redirect('/')
    }

    throw new Response('Failed to update completion status', { status })
  }

  if (intent === 'edit') {
    const title = String(formData.get('title') ?? '')
    const description = formData.get('description')
    const dueDate = formData.get('dueDate')
    const isCompleted = formData.get('currentIsCompleted') === 'true'

    const result = await updateTodo(id, {
      title,
      description: description ? String(description) : null,
      dueDate: dueDate ? String(dueDate) : null,
      isCompleted,
    })
    const status: number = result.status

    if (status === 200) {
      return null
    }

    if (status === 404) {
      return redirect('/')
    }

    if (status === 400) {
      return {
        fieldErrors: getFieldErrors(result.data),
        message: getMessage(result.data),
      }
    }

    throw new Response('Failed to update todo', { status })
  }

  throw new Response('Unknown action', { status: 400 })
}
