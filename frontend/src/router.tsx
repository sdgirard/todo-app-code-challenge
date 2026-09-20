import { createBrowserRouter } from 'react-router'
import { TodoListRoute } from './routes/TodoListRoute'
import { todoListLoader } from './routes/TodoListRoute.loader'
import { todoListAction } from './routes/TodoListRoute.action'
import { TodoDetailRoute } from './routes/TodoDetailRoute'
import { todoDetailLoader } from './routes/TodoDetailRoute.loader'
import { ErrorBoundary } from './routes/ErrorBoundary'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <TodoListRoute />,
    loader: todoListLoader,
    action: todoListAction,
  },
  {
    path: '/todos/:id',
    element: <TodoDetailRoute />,
    loader: todoDetailLoader,
    errorElement: <ErrorBoundary />,
  },
])
